import SwiftUI
import SwiftData

@main
struct FasoltApp: App {
    @State private var authService = AuthService()
    @State private var networkMonitor = NetworkMonitor()
    @Environment(\.scenePhase) private var scenePhase
    @State private var lastRefresh: Date = .distantPast

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
            .onChange(of: scenePhase) { oldPhase, newPhase in
                if newPhase == .active && oldPhase != .active {
                    let now = Date()
                    if now.timeIntervalSince(lastRefresh) > 30 {
                        lastRefresh = now
                        NotificationCenter.default.post(name: .appDidBecomeActive, object: nil)
                    }
                }
            }
        }
        .environment(authService)
        .environment(networkMonitor)
        .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
    }
}

extension Notification.Name {
    static let appDidBecomeActive = Notification.Name("appDidBecomeActive")
}
