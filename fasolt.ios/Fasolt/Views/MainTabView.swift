import SwiftUI
import SwiftData

struct MainTabView: View {
    @Environment(AuthService.self) private var authService
    @Environment(NetworkMonitor.self) private var networkMonitor
    @Environment(\.modelContext) private var modelContext

    @State private var syncService: SyncService?
    @State private var cardRepository: CardRepository?
    @State private var deckRepository: DeckRepository?
    @State private var notificationService: NotificationService?

    var body: some View {
        Group {
            if let cardRepository, let deckRepository, let notificationService {
                let studyViewModelFactory: () -> StudyViewModel = {
                    let vm = StudyViewModel(cardRepository: cardRepository)
                    vm.notificationService = notificationService
                    return vm
                }

                TabView {
                    DashboardView(
                        viewModel: DashboardViewModel(apiClient: authService.apiClient),
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
                        viewModel: CardListViewModel(apiClient: authService.apiClient)
                    )
                    .tabItem {
                        Label("Cards", systemImage: "rectangle.on.rectangle")
                    }

                    SettingsView(
                        viewModel: SettingsViewModel(apiClient: authService.apiClient),
                        notificationViewModel: NotificationSettingsViewModel(apiClient: authService.apiClient)
                    )
                    .tabItem {
                        Label("Settings", systemImage: "gear")
                    }
                }
            } else {
                ProgressView()
            }
        }
        .task {
            let apiClient = authService.apiClient
            cardRepository = CardRepository(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            deckRepository = DeckRepository(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            notificationService = NotificationService(apiClient: apiClient)

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
