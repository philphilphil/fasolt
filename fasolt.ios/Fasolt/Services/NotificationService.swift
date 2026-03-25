import Foundation
import UserNotifications
import UIKit

@MainActor
@Observable
final class NotificationService {
    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func requestPermissionAndRegister() async {
        let center = UNUserNotificationCenter.current()
        do {
            let granted = try await center.requestAuthorization(options: [.alert, .sound, .badge])
            if granted {
                UIApplication.shared.registerForRemoteNotifications()
            }
        } catch {
            print("Notification permission error: \(error)")
        }
    }

    func registerDeviceToken(_ tokenData: Data) async {
        let token = tokenData.map { String(format: "%02x", $0) }.joined()
        let endpoint = Endpoint(
            path: "/api/notifications/device-token",
            method: .put,
            body: DeviceTokenRequest(token: token)
        )
        do {
            try await apiClient.request(endpoint) as Void
        } catch {
            print("Failed to register device token: \(error)")
        }
    }

    func deleteDeviceToken() async {
        let endpoint = Endpoint(path: "/api/notifications/device-token", method: .delete)
        do {
            try await apiClient.request(endpoint) as Void
        } catch {
            print("Failed to delete device token: \(error)")
        }
    }

    func clearBadge() {
        UNUserNotificationCenter.current().setBadgeCount(0) { _ in }
    }
}
