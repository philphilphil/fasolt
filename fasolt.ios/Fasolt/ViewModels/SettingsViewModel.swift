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
    /// The authenticated session is the gate — the iOS UI handles confirmation.
    /// Throws `APIError` so the caller can render server messages.
    func deleteAccount() async throws {
        let endpoint = Endpoint(path: "/api/account", method: .delete)
        try await apiClient.request(endpoint)
        logger.info("Account deleted")
    }
}
