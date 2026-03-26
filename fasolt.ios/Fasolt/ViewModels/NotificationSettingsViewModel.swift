import Foundation
import UserNotifications

@MainActor
@Observable
final class NotificationSettingsViewModel {
    static let allowedIntervals = [4, 6, 8, 10, 12, 24]

    var intervalHours: Int = 8
    var hasDeviceToken = false
    var permissionStatus: UNAuthorizationStatus = .notDetermined
    var isLoading = false
    var errorMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func load() async {
        isLoading = true
        errorMessage = nil

        // Check system permission status
        let settings = await UNUserNotificationCenter.current().notificationSettings()
        permissionStatus = settings.authorizationStatus

        // Fetch server settings
        do {
            let endpoint = Endpoint(path: "/api/notifications/settings", method: .get)
            let response: NotificationSettingsResponse = try await apiClient.request(endpoint)
            intervalHours = response.intervalHours
            hasDeviceToken = response.hasDeviceToken
        } catch {
            errorMessage = "Could not load notification settings."
        }

        isLoading = false
    }

    func updateInterval(_ hours: Int) async {
        isLoading = true
        errorMessage = nil
        let endpoint = Endpoint(
            path: "/api/notifications/settings",
            method: .put,
            body: UpdateNotificationSettingsRequest(intervalHours: hours)
        )
        do {
            try await apiClient.request(endpoint)
            intervalHours = hours
        } catch {
            errorMessage = "Could not update notification interval."
        }
        isLoading = false
    }

    var permissionLabel: String {
        switch permissionStatus {
        case .authorized: return "Enabled"
        case .denied: return "Denied — tap to open Settings"
        case .provisional: return "Provisional"
        case .notDetermined: return "Not requested yet"
        case .ephemeral: return "Ephemeral"
        @unknown default: return "Unknown"
        }
    }

    var isPermissionDenied: Bool {
        permissionStatus == .denied
    }
}
