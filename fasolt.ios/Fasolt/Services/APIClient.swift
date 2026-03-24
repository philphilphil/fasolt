import Foundation
import os

private let apiLogger = Logger(subsystem: "com.fasolt.app", category: "API")

final class APIClient: @unchecked Sendable {
    private let session: URLSession
    private let keychain: KeychainHelper
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder

    private var refreshTask: Task<Bool, Error>?

    init(session: URLSession = .shared, keychain: KeychainHelper = KeychainHelper()) {
        self.session = session
        self.keychain = keychain
        self.encoder = JSONEncoder()
        self.decoder = JSONDecoder()
    }

    var baseURL: String? {
        keychain.retrieve("fasolt.serverURL")
    }

    // MARK: - Authenticated requests

    func request<T: Decodable>(_ endpoint: Endpoint) async throws -> T {
        let data = try await performRequest(endpoint, authenticated: true)
        do {
            return try decoder.decode(T.self, from: data)
        } catch {
            throw APIError.decodingError(error.localizedDescription)
        }
    }

    func request(_ endpoint: Endpoint) async throws {
        _ = try await performRequest(endpoint, authenticated: true)
    }

    // MARK: - Unauthenticated requests (for auth flow)

    func unauthenticatedRequest<T: Decodable>(_ endpoint: Endpoint) async throws -> T {
        let data = try await performRequest(endpoint, authenticated: false)
        do {
            return try decoder.decode(T.self, from: data)
        } catch {
            throw APIError.decodingError(error.localizedDescription)
        }
    }

    // MARK: - Form-encoded POST (for OAuth token endpoint)

    private static let formURLEncodedAllowed: CharacterSet = {
        var set = CharacterSet.alphanumerics
        set.insert(charactersIn: "-._~")
        return set
    }()

    func formPost<T: Decodable>(_ path: String, params: [String: String]) async throws -> T {
        guard let base = baseURL else { throw APIError.invalidURL }
        guard let url = URL(string: base + path) else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")

        let body = params
            .map { "\($0.key)=\($0.value.addingPercentEncoding(withAllowedCharacters: Self.formURLEncodedAllowed) ?? $0.value)" }
            .joined(separator: "&")
        request.httpBody = body.data(using: .utf8)

        let (data, response) = try await performRaw(request)
        try validateResponse(response)
        do {
            return try decoder.decode(T.self, from: data)
        } catch {
            throw APIError.decodingError(error.localizedDescription)
        }
    }

    // MARK: - Internal

    private func performRequest(_ endpoint: Endpoint, authenticated: Bool) async throws -> Data {
        guard let base = baseURL else { throw APIError.invalidURL }
        let url = try endpoint.url(baseURL: base)

        var request = URLRequest(url: url)
        request.httpMethod = endpoint.method.rawValue
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        if authenticated {
            try await injectAuth(&request)
        }

        if let body = endpoint.body {
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try encoder.encode(body)
        }

        let (data, response) = try await performRaw(request)

        if let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 401, authenticated {
            apiLogger.warning("401 for \(endpoint.path), attempting token refresh")
            if try await refreshTokenCoalesced() {
                apiLogger.info("Refresh succeeded, retrying \(endpoint.path)")
                try await injectAuth(&request)
                let (retryData, retryResponse) = try await performRaw(request)
                if let retryHttp = retryResponse as? HTTPURLResponse {
                    apiLogger.info("Retry \(endpoint.path): \(retryHttp.statusCode)")
                }
                try validateResponse(retryResponse)
                return retryData
            } else {
                apiLogger.error("Token refresh failed")
                throw APIError.unauthorized
            }
        }

        try validateResponse(response)
        return data
    }

    private func performRaw(_ request: URLRequest) async throws -> (Data, URLResponse) {
        do {
            return try await session.data(for: request)
        } catch {
            apiLogger.error("Network error for \(request.url?.path ?? "?"): \(error)")
            throw APIError.networkError(error.localizedDescription)
        }
    }

    private func injectAuth(_ request: inout URLRequest) async throws {
        guard let token = keychain.retrieve("fasolt.accessToken") else {
            throw APIError.unauthorized
        }

        if let expiryString = keychain.retrieve("fasolt.tokenExpiry"),
           let expiry = DateFormatters.iso8601.date(from: expiryString),
           expiry <= Date.now {
            if try await refreshTokenCoalesced() {
                guard let newToken = keychain.retrieve("fasolt.accessToken") else {
                    throw APIError.unauthorized
                }
                request.setValue("Bearer \(newToken)", forHTTPHeaderField: "Authorization")
                return
            } else {
                throw APIError.unauthorized
            }
        }

        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        apiLogger.debug("Token injected (length: \(token.count))")
    }

    private func refreshTokenCoalesced() async throws -> Bool {
        if let existing = refreshTask {
            return try await existing.value
        }
        let task = Task { [self] in try await doRefreshToken() }
        refreshTask = task
        do {
            let result = try await task.value
            refreshTask = nil
            return result
        } catch {
            refreshTask = nil
            throw error
        }
    }

    private func doRefreshToken() async throws -> Bool {
        guard let refreshToken = keychain.retrieve("fasolt.refreshToken"),
              let clientId = keychain.retrieve("fasolt.clientId") else {
            apiLogger.warning("No refresh token or client ID in keychain")
            return false
        }
        apiLogger.info("Refreshing token")

        let params: [String: String] = [
            "grant_type": "refresh_token",
            "refresh_token": refreshToken,
            "client_id": clientId,
        ]

        do {
            let tokenResponse: TokenResponse = try await formPost("/oauth/token", params: params)
            keychain.save(tokenResponse.accessToken, forKey: "fasolt.accessToken")
            apiLogger.info("Token refreshed successfully")
            if let newRefresh = tokenResponse.refreshToken {
                keychain.save(newRefresh, forKey: "fasolt.refreshToken")
            }
            let expiry = Date.now.addingTimeInterval(TimeInterval(tokenResponse.expiresIn))
            keychain.save(DateFormatters.iso8601.string(from: expiry), forKey: "fasolt.tokenExpiry")
            return true
        } catch {
            keychain.deleteAll()
            return false
        }
    }

    private func validateResponse(_ response: URLResponse) throws {
        guard let httpResponse = response as? HTTPURLResponse else { return }
        guard (200...299).contains(httpResponse.statusCode) else {
            apiLogger.error("HTTP \(httpResponse.statusCode) for \(httpResponse.url?.path ?? "?")")
            throw APIError.fromStatus(httpResponse.statusCode)
        }
    }
}
