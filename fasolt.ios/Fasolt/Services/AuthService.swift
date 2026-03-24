import Foundation
import AuthenticationServices
import CryptoKit
import UIKit

@Observable
final class AuthService {
    var isAuthenticated = false
    var isLoading = false
    var errorMessage: String?

    let keychain: KeychainHelper
    let apiClient: APIClient

    static let redirectURI = "fasolt://oauth/callback"
    static let defaultServerURL = "https://fasolt.app"

    init(keychain: KeychainHelper = KeychainHelper(), apiClient: APIClient = APIClient()) {
        self.keychain = keychain
        self.apiClient = apiClient
        self.isAuthenticated = keychain.retrieve("fasolt.accessToken") != nil
    }

    // MARK: - Public API

    var serverURL: String {
        keychain.retrieve("fasolt.serverURL") ?? Self.defaultServerURL
    }

    @MainActor
    func signIn(serverURL: String) async {
        isLoading = true
        errorMessage = nil

        keychain.save(serverURL, forKey: "fasolt.serverURL")

        do {
            let clientId = try await ensureClientRegistered(serverURL: serverURL)

            let codeVerifier = Self.generateCodeVerifier()
            let codeChallenge = Self.generateCodeChallenge(from: codeVerifier)

            let authCode = try await openAuthSession(
                serverURL: serverURL,
                clientId: clientId,
                codeChallenge: codeChallenge
            )

            try await exchangeCode(
                authCode,
                clientId: clientId,
                codeVerifier: codeVerifier
            )

            isAuthenticated = true
        } catch {
            errorMessage = "Could not connect. Check your server URL and try again."
        }

        isLoading = false
    }

    func signOut() {
        keychain.deleteAll()
        isAuthenticated = false
    }

    // MARK: - Client Registration

    @MainActor
    private func ensureClientRegistered(serverURL: String) async throws -> String {
        if let existingClientId = keychain.retrieve("fasolt.clientId") {
            return existingClientId
        }

        let body = ClientRegistrationRequest(
            clientName: "Fasolt iOS",
            redirectUris: [Self.redirectURI]
        )
        let endpoint = Endpoint(path: "/oauth/register", method: .post, body: body)
        let response: ClientRegistrationResponse = try await apiClient.unauthenticatedRequest(endpoint)

        keychain.save(response.clientId, forKey: "fasolt.clientId")
        return response.clientId
    }

    // MARK: - ASWebAuthenticationSession

    @MainActor
    private func openAuthSession(
        serverURL: String,
        clientId: String,
        codeChallenge: String
    ) async throws -> String {
        var components = URLComponents(string: serverURL + "/oauth/authorize")!
        components.queryItems = [
            URLQueryItem(name: "response_type", value: "code"),
            URLQueryItem(name: "client_id", value: clientId),
            URLQueryItem(name: "redirect_uri", value: Self.redirectURI),
            URLQueryItem(name: "code_challenge", value: codeChallenge),
            URLQueryItem(name: "code_challenge_method", value: "S256"),
            URLQueryItem(name: "scope", value: "offline_access"),
        ]

        let authURL = components.url!

        let callbackURL = try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<URL, Error>) in
            let session = ASWebAuthenticationSession(
                url: authURL,
                callbackURLScheme: "fasolt"
            ) { url, error in
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
            session.start()
        }

        guard let code = Self.extractCode(from: callbackURL) else {
            throw APIError.unauthorized
        }

        return code
    }

    // MARK: - Token Exchange

    @MainActor
    private func exchangeCode(_ code: String, clientId: String, codeVerifier: String) async throws {
        let params: [String: String] = [
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": Self.redirectURI,
            "client_id": clientId,
            "code_verifier": codeVerifier,
        ]

        let tokenResponse: TokenResponse = try await apiClient.formPost("/oauth/token", params: params)

        keychain.save(tokenResponse.accessToken, forKey: "fasolt.accessToken")
        if let refreshToken = tokenResponse.refreshToken {
            keychain.save(refreshToken, forKey: "fasolt.refreshToken")
        }
        let expiry = Date.now.addingTimeInterval(TimeInterval(tokenResponse.expiresIn))
        keychain.save(ISO8601DateFormatter().string(from: expiry), forKey: "fasolt.tokenExpiry")
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
