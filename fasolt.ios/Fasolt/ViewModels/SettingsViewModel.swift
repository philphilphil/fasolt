import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Settings")

@MainActor
@Observable
final class SettingsViewModel {
    var email: String?
    var displayName: String?
    var externalProvider: String?
    var serverURL: String?
    var isLoading = false
    var errorMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadUserInfo() async {
        isLoading = true
        errorMessage = nil

        serverURL = apiClient.baseURL

        do {
            let endpoint = Endpoint(path: "/api/account/me", method: .get)
            let userInfo: UserInfoResponse = try await apiClient.request(endpoint)
            email = userInfo.email
            displayName = userInfo.displayName
            externalProvider = userInfo.externalProvider
            logger.info("Loaded user info: \(userInfo.displayName ?? userInfo.email)")
        } catch {
            logger.error("Failed to load user info: \(error)")
            errorMessage = "Could not load account info."
            email = nil
            displayName = nil
            externalProvider = nil
        }

        isLoading = false
    }

    var appVersion: String {
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "?"
        let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "?"
        return "\(version) (\(build))"
    }

    /// Permanently deletes the signed-in user's account on the server.
    /// For external (GitHub/Apple) accounts pass `confirmIdentity`; for local accounts pass `password`.
    /// Throws `APIError` so the caller can render server validation messages.
    func deleteAccount(password: String? = nil, confirmIdentity: String? = nil) async throws {
        let body = DeleteAccountRequest(password: password, confirmIdentity: confirmIdentity)
        let endpoint = Endpoint(path: "/api/account", method: .delete, body: body)
        try await apiClient.request(endpoint)
        logger.info("Account deleted")
    }
}
