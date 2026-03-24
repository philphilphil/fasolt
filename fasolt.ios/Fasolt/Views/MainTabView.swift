import SwiftUI
import SwiftData

struct MainTabView: View {
    @Environment(AuthService.self) private var authService
    @Environment(NetworkMonitor.self) private var networkMonitor
    @Environment(\.modelContext) private var modelContext

    @State private var syncService: SyncService?

    var body: some View {
        let apiClient = authService.apiClient

        let cardRepository = CardRepository(
            apiClient: apiClient,
            networkMonitor: networkMonitor,
            modelContext: modelContext
        )

        let deckRepository = DeckRepository(
            apiClient: apiClient,
            networkMonitor: networkMonitor,
            modelContext: modelContext
        )

        let studyViewModelFactory: () -> StudyViewModel = {
            StudyViewModel(cardRepository: cardRepository)
        }

        TabView {
            DashboardView(
                viewModel: DashboardViewModel(apiClient: apiClient),
                studyViewModelFactory: studyViewModelFactory
            )
            .tabItem {
                Label("Dashboard", systemImage: "chart.bar")
            }

            DeckListView(
                viewModel: DeckListViewModel(deckRepository: deckRepository),
                deckRepository: deckRepository,
                studyViewModelFactory: studyViewModelFactory
            )
            .tabItem {
                Label("Decks", systemImage: "rectangle.stack")
            }

            CardListView(
                viewModel: CardListViewModel(apiClient: apiClient)
            )
            .tabItem {
                Label("Cards", systemImage: "rectangle.on.rectangle")
            }

            SettingsView(
                viewModel: SettingsViewModel(apiClient: apiClient)
            )
            .tabItem {
                Label("Settings", systemImage: "gear")
            }
        }
        .task {
            let service = SyncService(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            syncService = service
            await service.startMonitoring()
        }
    }
}
