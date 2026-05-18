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
    @State private var deckListViewModel: DeckListViewModel?
    @State private var cardListViewModel: CardListViewModel?
    @State private var studySession: StudySession?
    @State private var selectedTab: Int = 0
    @State private var router = NavigationRouter.shared

    var body: some View {
        Group {
            if let cardRepository, let deckRepository, let notificationService,
               let deckListViewModel, let cardListViewModel {
                let studyViewModelFactory: () -> StudyViewModel = {
                    let vm = StudyViewModel(cardRepository: cardRepository)
                    vm.notificationService = notificationService
                    return vm
                }

                let snapshotViewModel = SnapshotViewModel(apiClient: authService.apiClient)
                TabView(selection: $selectedTab) {
                    DashboardView(
                        viewModel: DashboardViewModel(apiClient: authService.apiClient, deckRepository: deckRepository)
                    )
                    .tabItem {
                        Label("Study", systemImage: "book.fill")
                    }
                    .tag(0)

                    LibraryView(
                        deckListViewModel: deckListViewModel,
                        cardListViewModel: cardListViewModel,
                        deckRepository: deckRepository,
                        cardRepository: cardRepository,
                        snapshotViewModel: snapshotViewModel
                    )
                    .tabItem {
                        Label("Library", systemImage: "books.vertical.fill")
                    }
                    .tag(1)

                    ProgressDashboardView(
                        viewModel: ProgressViewModel(apiClient: authService.apiClient)
                    )
                    .tabItem {
                        Label("Progress", systemImage: "chart.bar.fill")
                    }
                    .tag(2)

                    SettingsView(
                        viewModel: SettingsViewModel(apiClient: authService.apiClient),
                        notificationViewModel: NotificationSettingsViewModel(apiClient: authService.apiClient),
                        schedulingViewModel: SchedulingSettingsViewModel(apiClient: authService.apiClient)
                    )
                    .tabItem {
                        Label("Settings", systemImage: "gear")
                    }
                    .tag(3)
                }
                .fullScreenCover(item: $studySession, onDismiss: {
                    NotificationCenter.default.post(name: .studySessionEnded, object: nil)
                }) { session in
                    NavigationStack {
                        StudyView(viewModel: studyViewModelFactory(), deckId: session.deckId, mode: session.mode)
                    }
                }
                .environment(\.startStudy, StartStudyAction { deckId, mode in
                    studySession = StudySession(deckId: deckId, mode: mode)
                })
                .onAppear {
                    handlePendingDeepLink()
                }
                .onChange(of: router.pendingDeepLink) { _, _ in
                    handlePendingDeepLink()
                }
            } else {
                ProgressView()
            }
        }
        .task {
            let apiClient = authService.apiClient
            let cards = CardRepository(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            let decks = DeckRepository(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            cardRepository = cards
            deckRepository = decks
            notificationService = NotificationService(apiClient: apiClient)

            // Persist long-lived viewModels in @State so they survive MainTabView body
            // re-evaluations (which happen on every fullScreenCover toggle).
            if deckListViewModel == nil {
                deckListViewModel = DeckListViewModel(deckRepository: decks)
            }
            if cardListViewModel == nil {
                cardListViewModel = CardListViewModel(
                    apiClient: apiClient,
                    cardRepository: cards,
                    deckRepository: decks
                )
            }

            let service = SyncService(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            syncService = service
            await service.startMonitoring()
        }
    }

    private func handlePendingDeepLink() {
        guard let deepLink = router.pendingDeepLink else { return }
        switch deepLink {
        case .study:
            router.pendingDeepLink = nil
            selectedTab = 0
        }
    }
}
