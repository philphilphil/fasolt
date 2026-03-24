import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Settings")

@MainActor
@Observable
final class SettingsViewModel {
    var email: String?
    var displayName: String?
    var serverURL: String?
    var isLoading = false

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadUserInfo() async {
        isLoading = true

        serverURL = apiClient.baseURL

        do {
            let endpoint = Endpoint(path: "/api/account/me", method: .get)
            let userInfo: UserInfoResponse = try await apiClient.request(endpoint)
            email = userInfo.email
            displayName = userInfo.displayName
            logger.info("Loaded user info: \(userInfo.email)")
        } catch {
            logger.error("Failed to load user info: \(error)")
            email = nil
        }

        isLoading = false
    }

    var appVersion: String {
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "?"
        let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "?"
        return "\(version) (\(build))"
    }
}
