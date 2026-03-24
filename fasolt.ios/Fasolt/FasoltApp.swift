import SwiftUI
import SwiftData

@main
struct FasoltApp: App {
    @State private var authService = AuthService()

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
        }
        .environment(authService)
        .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
    }
}
