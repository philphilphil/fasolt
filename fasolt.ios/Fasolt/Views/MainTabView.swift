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
    @State private var showStudy = false
    @State private var studyDeckId: String?
    @State private var selectedTab: Int = 0
    @State private var router = NavigationRouter.shared

    var body: some View {
        Group {
            if let cardRepository, let deckRepository, let notificationService {
                let studyViewModelFactory: () -> StudyViewModel = {
                    let vm = StudyViewModel(cardRepository: cardRepository)
                    vm.notificationService = notificationService
                    return vm
                }

                TabView(selection: $selectedTab) {
                    DashboardView(
                        viewModel: DashboardViewModel(apiClient: authService.apiClient, deckRepository: deckRepository)
                    )
                    .tabItem {
                        Label("Study", systemImage: "book.fill")
                    }
                    .tag(0)

                    LibraryView(
                        deckListViewModel: DeckListViewModel(deckRepository: deckRepository),
                        cardListViewModel: CardListViewModel(
                            apiClient: authService.apiClient,
                            cardRepository: cardRepository,
                            deckRepository: deckRepository
                        ),
                        deckRepository: deckRepository,
                        cardRepository: cardRepository
                    )
                    .tabItem {
                        Label("Library", systemImage: "books.vertical.fill")
                    }
                    .tag(1)

                    SettingsView(
                        viewModel: SettingsViewModel(apiClient: authService.apiClient),
                        notificationViewModel: NotificationSettingsViewModel(apiClient: authService.apiClient),
                        schedulingViewModel: SchedulingSettingsViewModel(apiClient: authService.apiClient),
                        snapshotViewModel: SnapshotViewModel(apiClient: authService.apiClient)
                    )
                    .tabItem {
                        Label("Settings", systemImage: "gear")
                    }
                    .tag(2)
                }
                .fullScreenCover(isPresented: $showStudy, onDismiss: {
                    studyDeckId = nil
                    NotificationCenter.default.post(name: .studySessionEnded, object: nil)
                }) {
                    NavigationStack {
                        StudyView(viewModel: studyViewModelFactory(), deckId: studyDeckId)
                    }
                }
                .environment(\.startStudy, StartStudyAction { deckId in
                    studyDeckId = deckId
                    showStudy = true
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

    private func handlePendingDeepLink() {
        guard let deepLink = router.pendingDeepLink else { return }
        switch deepLink {
        case .study:
            router.pendingDeepLink = nil
            selectedTab = 0
        }
    }
}
