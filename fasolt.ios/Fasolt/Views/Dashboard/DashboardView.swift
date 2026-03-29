import SwiftUI

struct DashboardView: View {
    @Environment(\.startStudy) private var startStudy
    @State private var viewModel: DashboardViewModel

    init(viewModel: DashboardViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 16) {
                    heroCard
                    statsRow
                    if viewModel.totalCards > 0 {
                        stateBar
                    }
                    if viewModel.totalCards == 0 && !viewModel.isLoading && viewModel.errorMessage == nil {
                        ContentUnavailableView(
                            "No cards yet",
                            systemImage: "rectangle.on.rectangle",
                            description: Text("Create cards via MCP (or the UI) to get started")
                        )
                        .frame(maxWidth: .infinity)
                        .padding(.top, 32)
                    }
                    if !dueDecks.isEmpty {
                        deckSection
                    }
                }
                .padding()
            }
            .refreshable {
                await viewModel.loadStats()
            }
            .navigationTitle("Dashboard")
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

    private var heroCard: some View {
        VStack(spacing: 8) {
            Text("Cards due")
                .font(.subheadline)
                .foregroundStyle(.white.opacity(0.8))

            Text("\(viewModel.dueCount)")
                .font(.system(size: 48, weight: .bold, design: .rounded))
                .foregroundStyle(.white)

            if viewModel.dueCount > 0 {
                Text("Study Now")
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 20)
                    .padding(.vertical, 8)
                    .background(.white.opacity(0.2), in: RoundedRectangle(cornerRadius: 8))
            } else {
                Text("All caught up!")
                    .font(.subheadline)
                    .foregroundStyle(.white.opacity(0.7))
            }
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 28)
        .background(
            LinearGradient(
                colors: [.blue, .blue.opacity(0.8)],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            ),
            in: RoundedRectangle(cornerRadius: 16)
        )
        .onTapGesture {
            if viewModel.dueCount > 0 {
                startStudy()
            }
        }
    }

    private var statsRow: some View {
        HStack(spacing: 8) {
            statPill("Total", value: "\(viewModel.totalCards)")
            statPill("Today", value: "\(viewModel.studiedToday)")
            statPill("Decks", value: "\(viewModel.totalDecks)")
        }
    }

    private func statPill(_ label: String, value: String) -> some View {
        VStack(spacing: 4) {
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.title3.weight(.semibold))
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 12)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private var deckSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Study by deck")
                .font(.caption2)
                .textCase(.uppercase)
                .tracking(1)
                .foregroundStyle(.secondary)

            ForEach(dueDecks, id: \.id) { deck in
                Button {
                    startStudy(deckId: deck.id)
                } label: {
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(deck.name)
                                .font(.subheadline.weight(.medium))
                            Text("\(deck.cardCount) cards")
                                .font(.caption2)
                                .foregroundStyle(.secondary)
                        }
                        Spacer()
                        Text("\(deck.dueCount) due")
                            .font(.caption.weight(.medium))
                            .foregroundStyle(.orange)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 4)
                            .background(.orange.opacity(0.1), in: Capsule())
                    }
                    .padding(.vertical, 4)
                }
                .buttonStyle(.plain)
            }
        }
        .padding()
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private var stateBar: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("By state")
                .font(.caption2)
                .foregroundStyle(.secondary)

            GeometryReader { geo in
                HStack(spacing: 2) {
                    stateSegment(key: "new", color: .green, totalWidth: geo.size.width)
                    stateSegment(key: "review", color: .blue, totalWidth: geo.size.width)
                    stateSegment(key: "learning", color: .orange, totalWidth: geo.size.width)
                    stateSegment(key: "relearning", color: .red, totalWidth: geo.size.width)
                }
            }
            .frame(height: 6)
            .clipShape(Capsule())

            HStack(spacing: 12) {
                stateLabel("New", key: "new", color: .green)
                stateLabel("Review", key: "review", color: .blue)
                stateLabel("Learning", key: "learning", color: .orange)
                stateLabel("Relearn", key: "relearning", color: .red)
                Spacer()
            }
        }
        .padding()
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private func stateSegment(key: String, color: Color, totalWidth: CGFloat) -> some View {
        let count = viewModel.cardsByState[key] ?? 0
        let fraction = viewModel.totalCards > 0 ? CGFloat(count) / CGFloat(viewModel.totalCards) : 0
        return Rectangle()
            .fill(color)
            .frame(width: max(fraction * totalWidth, count > 0 ? 2 : 0))
    }

    private func stateLabel(_ label: String, key: String, color: Color) -> some View {
        HStack(spacing: 4) {
            Circle().fill(color).frame(width: 6, height: 6)
            Text("\(label) \(viewModel.cardsByState[key] ?? 0)")
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
    }
}
