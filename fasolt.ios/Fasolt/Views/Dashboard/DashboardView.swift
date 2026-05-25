import SwiftUI

struct DashboardView: View {
    @Environment(\.startStudy) private var startStudy
    @Environment(AuthService.self) private var authService
    @State private var viewModel: DashboardViewModel
    @State private var showMcpSheet = false

    init(viewModel: DashboardViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 14) {
                    if (viewModel.studyStats?.totalAnswered ?? 0) > 0 {
                        streakStrip
                    }
                    heroCard
                    if viewModel.totalCards == 0 && !viewModel.isLoading && viewModel.errorMessage == nil {
                        emptyStatePrompt
                    }
                    if !dueDecks.isEmpty {
                        deckSection
                    }
                }
                .padding(.horizontal, FasoltTheme.pagePadding)
                .padding(.top, 8)
                .padding(.bottom, 24)
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .scrollContentBackground(.hidden)
            .sheet(isPresented: $showMcpSheet) {
                NavigationStack {
                    List {
                        McpSetupSection(serverURL: authService.serverURL)
                    }
                    .navigationTitle("Connect your AI")
                    .navigationBarTitleDisplayMode(.inline)
                    .toolbar {
                        ToolbarItem(placement: .topBarTrailing) {
                            Button("Done") { showMcpSheet = false }
                        }
                    }
                }
            }
            .refreshable {
                await viewModel.loadStats()
            }
            .toolbar(.hidden, for: .navigationBar)
            .overlay {
                if viewModel.isLoading && viewModel.totalCards == 0 {
                    ProgressView()
                }
            }
            .overlay {
                if let error = viewModel.errorMessage, viewModel.totalCards == 0 {
                    ContentUnavailableView {
                        Label("Could not load", systemImage: "wifi.slash")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Retry") {
                            Task { await viewModel.loadStats() }
                        }
                    }
                }
            }
            .task {
                await viewModel.loadStats()
            }
            .onAppear {
                if viewModel.totalCards > 0 || viewModel.errorMessage != nil {
                    Task { await viewModel.loadStats() }
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: .appDidBecomeActive)) { _ in
                Task { await viewModel.loadStats() }
            }
            .onReceive(NotificationCenter.default.publisher(for: .studySessionEnded)) { _ in
                Task { await viewModel.loadStats() }
            }
            .offlineBanner()
        }
    }

    private var dueDecks: [DeckDTO] {
        viewModel.decks.filter { !$0.isSuspended && $0.dueCount > 0 }
    }

    private var dueDeckCount: Int { dueDecks.count }

    // MARK: - Hero card

    private var heroCard: some View {
        ZStack(alignment: .top) {
            RoundedRectangle(cornerRadius: FasoltTheme.cardRadius, style: .continuous)
                .fill(FasoltTheme.paper1)
                .overlay(
                    RoundedRectangle(cornerRadius: FasoltTheme.cardRadius, style: .continuous)
                        .strokeBorder(FasoltTheme.rule2, lineWidth: FasoltTheme.hairline)
                )
                .shadow(color: .black.opacity(0.04), radius: 8, x: 0, y: 2)

            VStack(spacing: 0) {
                AccentStripe(horizontalInset: 24)

                VStack(alignment: .leading, spacing: 18) {
                    HStack(alignment: .bottom) {
                        VStack(alignment: .leading, spacing: 4) {
                            CapsLabel(text: "Due", color: FasoltTheme.accent)
                            Text("\(viewModel.dueCount)")
                                .font(.system(size: 64, weight: .semibold, design: .default))
                                .monospacedDigit()
                                .foregroundStyle(FasoltTheme.ink0)
                                .kerning(-1)
                                .padding(.top, 2)
                                .lineLimit(1)
                                .minimumScaleFactor(0.6)
                        }
                        Spacer(minLength: 8)
                        VStack(alignment: .trailing, spacing: 2) {
                            Text("cards across")
                                .font(.system(size: 14))
                                .foregroundStyle(FasoltTheme.ink1)
                            Text("\(dueDeckCount) \(dueDeckCount == 1 ? "deck" : "decks")")
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(FasoltTheme.ink0)
                        }
                        .padding(.bottom, 8)
                    }

                    Button {
                        if viewModel.dueCount > 0 { startStudy() }
                    } label: {
                        HStack(spacing: 8) {
                            Text(viewModel.dueCount > 0 ? "Start reviewing" : "All caught up")
                            if viewModel.dueCount > 0 {
                                Image(systemName: "arrow.right")
                                    .font(.system(size: 14, weight: .bold))
                            }
                        }
                    }
                    .buttonStyle(AccentButtonStyle())
                    .disabled(viewModel.dueCount == 0)
                    .opacity(viewModel.dueCount == 0 ? 0.5 : 1)
                }
                .padding(.horizontal, 22)
                .padding(.top, 22)
                .padding(.bottom, 18)
            }
        }
        .padding(.top, 8)
    }

    // MARK: - Streak strip

    private var streakStrip: some View {
        HStack(spacing: 12) {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Text("\(viewModel.studyStats?.currentStreak ?? 0)")
                    .font(.system(size: 22, weight: .semibold))
                    .monospacedDigit()
                    .foregroundStyle(FasoltTheme.ink0)
                Text("day streak")
                    .font(.system(size: 13))
                    .foregroundStyle(FasoltTheme.ink2)
            }

            dayDots

            VStack(alignment: .trailing) {
                CapsLabel(text: "best \(viewModel.studyStats?.bestStreak ?? 0)", size: 10)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .paperCard()
    }

    // Last 7 days of real study activity, oldest→newest. Filled = studied that
    // day; hollow = didn't; today is ringed. The streak number beside it remains
    // the authoritative streak (rest days with nothing due read as hollow here).
    private var dayDots: some View {
        let days = viewModel.studyStats?.last7Days ?? []
        return HStack(spacing: 8) {
            ForEach(Array(days.enumerated()), id: \.element.id) { i, day in
                dayDot(studied: day.count > 0, isToday: i == days.count - 1)
            }
        }
        .frame(maxWidth: .infinity)
    }

    private func dayDot(studied: Bool, isToday: Bool) -> some View {
        ZStack {
            if studied {
                Circle()
                    .fill(FasoltTheme.accent)
                    .frame(width: 10, height: 10)
            } else {
                Circle()
                    .strokeBorder(FasoltTheme.rule2, lineWidth: 1.5)
                    .frame(width: 10, height: 10)
            }
            if isToday {
                Circle()
                    .strokeBorder(FasoltTheme.accent.opacity(0.5), lineWidth: 1.5)
                    .frame(width: 18, height: 18)
            }
        }
        .frame(width: 18, height: 18)
    }

    // MARK: - Empty state

    private var emptyStatePrompt: some View {
        VStack(spacing: 14) {
            VStack(spacing: 12) {
                Image(systemName: "sparkles")
                    .font(.system(size: 28, weight: .semibold))
                    .foregroundStyle(FasoltTheme.accent)
                    .padding(.top, 6)

                Text("Connect your AI")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundStyle(FasoltTheme.ink0)

                Text("Add Fasolt to Claude, ChatGPT, or another MCP client to start creating cards from your notes.")
                    .font(.system(size: 14))
                    .foregroundStyle(FasoltTheme.ink2)
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)

                Button {
                    showMcpSheet = true
                } label: {
                    Text("Set up now")
                }
                .buttonStyle(AccentButtonStyle(height: 44, radius: 12, fontSize: 15))
                .padding(.top, 4)
            }
            .padding(20)
            .paperCard()

            Button {
                Task { await viewModel.createDemoDeck() }
            } label: {
                if viewModel.isCreatingDemo {
                    ProgressView()
                } else {
                    Text("or — just try a demo deck")
                        .font(.system(size: 13))
                        .foregroundStyle(FasoltTheme.ink2)
                }
            }
            .buttonStyle(.borderless)
            .disabled(viewModel.isCreatingDemo)
        }
        .padding(.top, 4)
    }

    // MARK: - Deck section

    private var deckSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            CapsLabel(text: "Decks due", size: 12)
                .padding(.horizontal, 16)
                .padding(.top, 8)

            VStack(spacing: 0) {
                ForEach(Array(dueDecks.enumerated()), id: \.element.id) { index, deck in
                    Button {
                        startStudy(deckId: deck.id)
                    } label: {
                        deckRow(deck: deck, isLast: index == dueDecks.count - 1)
                    }
                    .buttonStyle(.plain)
                }
            }
            .paperCard()
        }
    }

    private func deckRow(deck: DeckDTO, isLast: Bool) -> some View {
        HStack(spacing: 12) {
            DeckTag(color: FasoltTheme.deckColor(for: deck.id), size: 10)
            VStack(alignment: .leading, spacing: 2) {
                Text(deck.name)
                    .font(.system(size: 16, weight: .medium))
                    .foregroundStyle(FasoltTheme.ink0)
                    .lineLimit(1)
                Text("\(deck.cardCount) cards")
                    .font(.system(size: 13))
                    .foregroundStyle(FasoltTheme.ink2)
            }
            Spacer(minLength: 8)
            Text("\(deck.dueCount) due")
                .font(.system(size: 13, weight: .semibold))
                .monospacedDigit()
                .foregroundStyle(FasoltTheme.accentHi)
                .padding(.horizontal, 10)
                .padding(.vertical, 4)
                .background(
                    Capsule().fill(FasoltTheme.accentSoft)
                )
            Image(systemName: "chevron.right")
                .font(.system(size: 12, weight: .bold))
                .foregroundStyle(FasoltTheme.ink3)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .overlay(alignment: .bottom) {
            if !isLast {
                Rectangle()
                    .fill(FasoltTheme.rule2)
                    .frame(height: FasoltTheme.hairline)
                    .padding(.leading, 38)
            }
        }
    }
}
