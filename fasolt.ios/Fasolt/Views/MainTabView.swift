import SwiftUI
import SwiftData

struct MainTabView: View {
    @Environment(AuthService.self) private var authService
    @Environment(NetworkMonitor.self) private var networkMonitor
    @Environment(\.modelContext) private var modelContext

    var body: some View {
        TabView {
            DashboardView(
                viewModel: DashboardViewModel(apiClient: authService.apiClient),
                studyViewModelFactory: {
                    StudyViewModel(
                        cardRepository: CardRepository(
                            apiClient: authService.apiClient,
                            networkMonitor: networkMonitor,
                            modelContext: modelContext
                        )
                    )
                }
            )
            .tabItem {
                Label("Dashboard", systemImage: "chart.bar")
            }

            DeckListView()
                .tabItem {
                    Label("Decks", systemImage: "rectangle.stack")
                }

            SettingsView()
                .tabItem {
                    Label("Settings", systemImage: "gear")
                }
        }
    }
}
