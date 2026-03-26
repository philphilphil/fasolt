import Foundation
import UserNotifications
import UIKit
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Notifications")

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
            logger.info("Notification permission granted: \(granted)")
            if granted {
                logger.info("Calling registerForRemoteNotifications")
                UIApplication.shared.registerForRemoteNotifications()
            }
        } catch {
            logger.error("Notification permission error: \(error)")
        }
    }

    func registerDeviceToken(_ tokenData: Data) async {
        let token = tokenData.map { String(format: "%02x", $0) }.joined()
        logger.info("Registering device token: \(token.prefix(16))...")
        let endpoint = Endpoint(
            path: "/api/notifications/device-token",
            method: .put,
            body: DeviceTokenRequest(token: token)
        )
        do {
            try await apiClient.request(endpoint)
            logger.info("Device token registered successfully")
        } catch {
            logger.error("Failed to register device token: \(error)")
        }
    }

    func deleteDeviceToken() async {
        logger.info("Deleting device token")
        let endpoint = Endpoint(path: "/api/notifications/device-token", method: .delete)
        do {
            try await apiClient.request(endpoint)
            logger.info("Device token deleted")
        } catch {
            logger.error("Failed to delete device token: \(error)")
        }
    }

    func clearBadge() {
        UNUserNotificationCenter.current().setBadgeCount(0) { _ in }
    }
}
