import SwiftUI
import SwiftData
import UserNotifications
import os

private let appLogger = Logger(subsystem: "com.fasolt.app", category: "AppDelegate")

class AppDelegate: NSObject, UIApplicationDelegate, @preconcurrency UNUserNotificationCenterDelegate {
    var onDeviceToken: ((Data) -> Void)?

    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil) -> Bool {
        UNUserNotificationCenter.current().delegate = self
        return true
    }

    func application(_ application: UIApplication,
                     didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        let token = deviceToken.map { String(format: "%02x", $0) }.joined()
        appLogger.info("Got APNs device token: \(token.prefix(16))...")
        onDeviceToken?(deviceToken)
    }

    func application(_ application: UIApplication,
                     didFailToRegisterForRemoteNotificationsWithError error: Error) {
        appLogger.error("Remote notification registration failed: \(error)")
    }

    // Show notifications as banners even when app is in foreground.
    func userNotificationCenter(_ center: UNUserNotificationCenter,
                                willPresent notification: UNNotification,
                                withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .sound, .badge])
    }

    // Handle user tapping a notification — deep link into the study screen.
    func userNotificationCenter(_ center: UNUserNotificationCenter,
                                didReceive response: UNNotificationResponse,
                                withCompletionHandler completionHandler: @escaping () -> Void) {
        defer { completionHandler() }
        guard response.actionIdentifier == UNNotificationDefaultActionIdentifier else { return }
        appLogger.info("Notification tapped — requesting study view")
        NavigationRouter.shared.pendingDeepLink = .study
    }
}

/// Holds a pending deep link request so that navigation survives between the
/// notification delegate callback (which may fire during cold launch before
/// SwiftUI views have mounted) and the view hierarchy becoming ready.
@MainActor
@Observable
final class NavigationRouter {
    static let shared = NavigationRouter()

    enum DeepLink: Equatable {
        case study
    }

    var pendingDeepLink: DeepLink?

    private init() {}
}

@main
struct FasoltApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @State private var authService = AuthService()
    @State private var featureFlags = FeatureFlagsService()

    @State private var networkMonitor = NetworkMonitor()
    @Environment(\.scenePhase) private var scenePhase
    @State private var lastRefresh: Date = .distantPast
    @State private var notificationService: NotificationService?

    var body: some Scene {
        WindowGroup {
            Group {
                if authService.isAuthenticated {
                    MainTabView()
                } else {
                    OnboardingView()
                }
            }
            .animation(.default, value: authService.isAuthenticated)
            .task {
                await featureFlags.refresh(serverURL: authService.serverURL)
            }
            .onChange(of: scenePhase) { oldPhase, newPhase in
                if newPhase == .active && oldPhase != .active {
                    let now = Date()
                    if now.timeIntervalSince(lastRefresh) > 30 {
                        lastRefresh = now
                        NotificationCenter.default.post(name: .appDidBecomeActive, object: nil)
                    }
                    notificationService?.clearBadge()
                }
            }
            .onAppear {
                if notificationService == nil {
                    let service = NotificationService(apiClient: authService.apiClient)
                    notificationService = service
                    appDelegate.onDeviceToken = { [weak authService] tokenData in
                        Task {
                            guard let authService, authService.isAuthenticated else {
                                appLogger.info("Skipping device token registration — not authenticated")
                                return
                            }
                            await service.registerDeviceToken(tokenData)
                        }
                    }
                }
                // Re-register for push on every launch if permission was already granted and user is logged in
                if authService.isAuthenticated {
                    Task {
                        let settings = await UNUserNotificationCenter.current().notificationSettings()
                        if settings.authorizationStatus == .authorized {
                            appLogger.info("Push permission already granted, re-registering")
                            await MainActor.run {
                                UIApplication.shared.registerForRemoteNotifications()
                            }
                        }
                    }
                }
            }
        }
        .environment(authService)
        .environment(featureFlags)
        .environment(networkMonitor)
        .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
    }
}

extension Notification.Name {
    static let appDidBecomeActive = Notification.Name("appDidBecomeActive")
    static let studySessionEnded = Notification.Name("studySessionEnded")
    static let sessionDidInvalidate = Notification.Name("sessionDidInvalidate")
}
