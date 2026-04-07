import Foundation
import AuthenticationServices
import CryptoKit
import UIKit
import os

private let authLogger = Logger(subsystem: "com.fasolt.app", category: "Auth")

@MainActor
@Observable
final class AuthService {
    var isAuthenticated = false
    var isLoading = false
    var errorMessage: String?

    let keychain: KeychainHelper
    let apiClient: APIClient
    private var activeAuthSession: ASWebAuthenticationSession?
    private var sessionInvalidationObserver: Any?

    static let redirectURI = "fasolt://oauth/callback"
    static let firstPartyClientId = "fasolt-ios"
    static let defaultServerURL = "https://fasolt.app"

    init(keychain: KeychainHelper = KeychainHelper(), apiClient: APIClient = APIClient()) {
        self.keychain = keychain
        self.apiClient = apiClient
        self.isAuthenticated = {
            guard keychain.retrieve("fasolt.accessToken") != nil else { return false }
            // If token is expired, we need a refresh token to recover
            if let expiryString = keychain.retrieve("fasolt.tokenExpiry"),
               let expiry = DateFormatters.parseISO8601( expiryString),
               expiry <= Date.now {
                return keychain.retrieve("fasolt.refreshToken") != nil
            }
            return true
        }()

        // Force logout when token refresh fails so the user isn't stuck on authenticated screens.
        // Uses NotificationCenter (synchronous, no race condition) instead of an async closure.
        sessionInvalidationObserver = NotificationCenter.default.addObserver(
            forName: .sessionDidInvalidate,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            guard let self, self.isAuthenticated else { return }
            authLogger.warning("Session invalidated — forcing logout")
            self.isAuthenticated = false
        }
    }

    // MARK: - Public API

    var serverURL: String {
        keychain.retrieve("fasolt.serverURL") ?? Self.defaultServerURL
    }

    func signIn(serverURL: String, providerHint: String? = nil) async {
        isLoading = true
        errorMessage = nil

        // Save server URL before auth — only clear old tokens after successful exchange
        let previousServerURL = keychain.retrieve("fasolt.serverURL")
        keychain.save(serverURL, forKey: "fasolt.serverURL")

        do {
            authLogger.info("Starting sign-in to \(serverURL)")
            let clientId = Self.firstPartyClientId
            authLogger.info("Using first-party client: \(clientId)")

            let codeVerifier = Self.generateCodeVerifier()
            let codeChallenge = Self.generateCodeChallenge(from: codeVerifier)

            authLogger.info("Opening auth session")
            let authCode = try await openAuthSession(
                serverURL: serverURL,
                clientId: clientId,
                codeChallenge: codeChallenge,
                providerHint: providerHint
            )
            authLogger.info("Got auth code, exchanging for token")

            try await exchangeCode(
                authCode,
                clientId: clientId,
                codeVerifier: codeVerifier
            )

            authLogger.info("Sign-in complete")
            isAuthenticated = true
        } catch let error as ASWebAuthenticationSessionError where error.code == .canceledLogin {
            authLogger.info("User cancelled sign-in")
            // Restore previous server URL if user cancelled
            if let previousServerURL {
                keychain.save(previousServerURL, forKey: "fasolt.serverURL")
            } else {
                keychain.delete("fasolt.serverURL")
            }
            errorMessage = nil
        } catch {
            authLogger.error("Sign-in failed: \(error)")
            // Restore previous server URL on failure
            if let previousServerURL {
                keychain.save(previousServerURL, forKey: "fasolt.serverURL")
            } else {
                keychain.delete("fasolt.serverURL")
            }
            errorMessage = "Could not connect. Check your server URL and try again."
        }

        isLoading = false
    }

    var registrationSuccess = false

    func register(email: String, password: String, serverURL: String) async {
        isLoading = true
        errorMessage = nil
        registrationSuccess = false

        let previousServerURL = keychain.retrieve("fasolt.serverURL")
        keychain.save(serverURL, forKey: "fasolt.serverURL")

        do {
            let body = RegisterRequest(email: email, password: password)
            let endpoint = Endpoint(path: "/api/identity/register", method: .post, body: body)
            try await apiClient.unauthenticatedRequest(endpoint)

            authLogger.info("Registration succeeded")
            registrationSuccess = true
        } catch {
            restoreServerURL(previous: previousServerURL)
            if let apiError = error as? APIError {
                errorMessage = Self.registrationErrorMessage(from: apiError)
            } else {
                errorMessage = "Registration failed. Please try again."
            }
        }

        isLoading = false
    }

    // MARK: - Apple Sign-In

    func signInWithApple(identityToken: String, serverURL: String) async {
        isLoading = true
        errorMessage = nil

        let previousServerURL = keychain.retrieve("fasolt.serverURL")
        keychain.save(serverURL, forKey: "fasolt.serverURL")

        do {
            let params: [String: String] = [
                "grant_type": "urn:fasolt:apple",
                "client_id": Self.firstPartyClientId,
                "identity_token": identityToken,
            ]

            let tokenResponse: TokenResponse = try await apiClient.formPost("/oauth/token", params: params)
            keychain.save(Self.firstPartyClientId, forKey: "fasolt.clientId")
            keychain.save(tokenResponse.accessToken, forKey: "fasolt.accessToken")
            if let refreshToken = tokenResponse.refreshToken {
                keychain.save(refreshToken, forKey: "fasolt.refreshToken")
            }
            let expiry = Date.now.addingTimeInterval(TimeInterval(tokenResponse.expiresIn))
            keychain.save(DateFormatters.formatISO8601(expiry), forKey: "fasolt.tokenExpiry")
            authLogger.info("Apple sign-in complete")
            isAuthenticated = true
        } catch {
            authLogger.error("Apple sign-in failed: \(error)")
            restoreServerURL(previous: previousServerURL)
            errorMessage = "Could not sign in with Apple. Please try again."
        }

        isLoading = false
    }

    private static func registrationErrorMessage(from error: APIError) -> String {
        switch error {
        case .badRequest(let detail):
            guard let detail else { return "Registration failed. Please try again." }
            let lower = detail.lowercased()
            if lower.contains("duplicate") || lower.contains("already taken") {
                return "An account with this email already exists."
            }
            if lower.contains("too short") || lower.contains("too few") {
                return "Password must be at least 8 characters."
            }
            if lower.contains("uppercase") {
                return "Password must contain an uppercase letter."
            }
            if lower.contains("lowercase") {
                return "Password must contain a lowercase letter."
            }
            if lower.contains("digit") || lower.contains("number") {
                return "Password must contain a number."
            }
            if lower.contains("email") {
                return "Please enter a valid email address."
            }
            return detail
        case .serverError(_, let detail):
            return detail ?? "Registration failed. Please try again."
        case .networkError:
            return "Could not connect. Check your internet connection."
        default:
            return "Registration failed. Please try again."
        }
    }

    private func restoreServerURL(previous: String?) {
        if let previous {
            keychain.save(previous, forKey: "fasolt.serverURL")
        } else {
            keychain.delete("fasolt.serverURL")
        }
    }

    func signOut() async {
        let service = NotificationService(apiClient: apiClient)
        await service.deleteDeviceToken()
        keychain.deleteAll()
        isAuthenticated = false
    }

    // MARK: - ASWebAuthenticationSession

    private func openAuthSession(
        serverURL: String,
        clientId: String,
        codeChallenge: String,
        providerHint: String? = nil
    ) async throws -> String {
        guard var components = URLComponents(string: serverURL + "/oauth/authorize") else {
            throw APIError.invalidURL
        }
        var queryItems: [URLQueryItem] = [
            URLQueryItem(name: "response_type", value: "code"),
            URLQueryItem(name: "client_id", value: clientId),
            URLQueryItem(name: "redirect_uri", value: Self.redirectURI),
            URLQueryItem(name: "code_challenge", value: codeChallenge),
            URLQueryItem(name: "code_challenge_method", value: "S256"),
            URLQueryItem(name: "scope", value: "offline_access"),
        ]
        if let providerHint {
            queryItems.append(URLQueryItem(name: "provider_hint", value: providerHint))
        }
        components.queryItems = queryItems

        guard let authURL = components.url else {
            throw APIError.invalidURL
        }

        let callbackURL = try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<URL, Error>) in
            let session = ASWebAuthenticationSession(
                url: authURL,
                callbackURLScheme: "fasolt"
            ) { [weak self] url, error in
                self?.activeAuthSession = nil
                if let error {
                    continuation.resume(throwing: error)
                } else if let url {
                    continuation.resume(returning: url)
                } else {
                    continuation.resume(throwing: APIError.unauthorized)
                }
            }
            session.presentationContextProvider = PresentationContextProvider.shared
            session.prefersEphemeralWebBrowserSession = false
            self.activeAuthSession = session
            session.start()
        }

        guard let code = Self.extractCode(from: callbackURL) else {
            throw APIError.unauthorized
        }

        return code
    }

    // MARK: - Token Exchange

    private func exchangeCode(_ code: String, clientId: String, codeVerifier: String) async throws {
        let params: [String: String] = [
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": Self.redirectURI,
            "client_id": clientId,
            "code_verifier": codeVerifier,
        ]

        let tokenResponse: TokenResponse = try await apiClient.formPost("/oauth/token", params: params)

        keychain.save(clientId, forKey: "fasolt.clientId")
        keychain.save(tokenResponse.accessToken, forKey: "fasolt.accessToken")
        if let refreshToken = tokenResponse.refreshToken {
            keychain.save(refreshToken, forKey: "fasolt.refreshToken")
        }
        let expiry = Date.now.addingTimeInterval(TimeInterval(tokenResponse.expiresIn))
        keychain.save(DateFormatters.formatISO8601( expiry), forKey: "fasolt.tokenExpiry")
        authLogger.info("Token exchanged, expires at \(DateFormatters.formatISO8601( expiry))")
    }

    // MARK: - PKCE Helpers (static for testing)

    static func generateCodeVerifier() -> String {
        var bytes = [UInt8](repeating: 0, count: 32)
        _ = SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes)
        return Data(bytes)
            .base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }

    static func generateCodeChallenge(from verifier: String) -> String {
        let data = Data(verifier.utf8)
        let hash = SHA256.hash(data: data)
        return Data(hash)
            .base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }

    static func extractCode(from url: URL) -> String? {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?
            .queryItems?
            .first(where: { $0.name == "code" })?
            .value
    }
}

// MARK: - Presentation Context Provider for ASWebAuthenticationSession

final class PresentationContextProvider: NSObject, ASWebAuthenticationPresentationContextProviding {
    static let shared = PresentationContextProvider()

    @MainActor
    func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        guard let scene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
              let window = scene.windows.first else {
            return ASPresentationAnchor()
        }
        return window
    }
}
