import SwiftUI

struct ProgressDashboardView: View {
    @State private var viewModel: ProgressViewModel

    init(viewModel: ProgressViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 16) {
                    if let progress = viewModel.progress {
                        statCards(progress)
                        periodStats(progress)
                        activityGrid(progress)
                        if progress.totalAnswered == 0 {
                            emptyHint
                        }
                    }
                }
                .padding()
            }
            .navigationTitle("Progress")
            .navigationBarTitleDisplayMode(.inline)
            .refreshable { await viewModel.load() }
            .overlay {
                if viewModel.isLoading && viewModel.progress == nil {
                    ProgressView()
                }
            }
            .overlay {
                if let error = viewModel.errorMessage, viewModel.progress == nil {
                    ContentUnavailableView {
                        Label("Could not load", systemImage: "wifi.slash")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Retry") { Task { await viewModel.load() } }
                    }
                }
            }
            .task { await viewModel.load() }
            .onReceive(NotificationCenter.default.publisher(for: .appDidBecomeActive)) { _ in
                Task { await viewModel.load() }
            }
            .onReceive(NotificationCenter.default.publisher(for: .studySessionEnded)) { _ in
                Task { await viewModel.load() }
            }
            .offlineBanner()
        }
    }

    private func statCards(_ p: ProgressDTO) -> some View {
        LazyVGrid(
            columns: [GridItem(.flexible(), spacing: 10), GridItem(.flexible(), spacing: 10)],
            spacing: 10
        ) {
            statCell(label: "CURRENT STREAK") {
                HStack(spacing: 5) {
                    if p.currentStreak > 0 {
                        Image(systemName: "flame.fill")
                            .foregroundStyle(.orange)
                            .font(.title3)
                    }
                    Text("\(p.currentStreak)")
                        .font(.title2.weight(.bold))
                        .monospacedDigit()
                }
            }
            statCell(label: "BEST STREAK") {
                Text("\(p.bestStreak)")
                    .font(.title2.weight(.bold))
                    .monospacedDigit()
            }
            statCell(label: "TOTAL ANSWERED") {
                Text("\(p.totalAnswered)")
                    .font(.title2.weight(.bold))
                    .monospacedDigit()
            }
            statCell(label: "TODAY") {
                Text("\(p.answeredToday)")
                    .font(.title2.weight(.bold))
                    .monospacedDigit()
            }
        }
    }

    @ViewBuilder
    private func statCell<V: View>(label: String, @ViewBuilder content: () -> V) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
                .tracking(0.6)
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private func periodStats(_ p: ProgressDTO) -> some View {
        HStack(spacing: 10) {
            periodCell(label: "THIS WEEK", value: p.answeredThisWeek)
            periodCell(label: "THIS MONTH", value: p.answeredThisMonth)
        }
    }

    private func periodCell(label: String, value: Int) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
                .tracking(0.6)
            Text("\(value)")
                .font(.title3.weight(.semibold))
                .monospacedDigit()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    // MARK: - Activity grid (GitHub-style)

    private func activityGrid(_ p: ProgressDTO) -> some View {
        let maxCount = max(1, p.dailyActivity.map { $0.count }.max() ?? 1)
        let cols = Array(repeating: GridItem(.flexible(), spacing: 6), count: 6)

        return VStack(alignment: .leading, spacing: 10) {
            Text("LAST 30 DAYS")
                .font(.caption2)
                .foregroundStyle(.secondary)
                .tracking(0.6)

            LazyVGrid(columns: cols, spacing: 6) {
                ForEach(Array(p.dailyActivity.enumerated()), id: \.element.id) { idx, day in
                    let isToday = idx == p.dailyActivity.count - 1
                    activityCell(day: day, isToday: isToday, maxCount: maxCount)
                }
            }

            HStack(spacing: 12) {
                legendSwatch(color: cellFill(count: 1, hadDue: false, isToday: false, maxCount: 1), label: "Studied")
                legendSwatch(color: cellFill(count: 0, hadDue: true, isToday: false, maxCount: 1), label: "Missed")
                legendSwatch(color: cellFill(count: 0, hadDue: false, isToday: false, maxCount: 1), label: "Rest")
                legendSwatch(color: .blue, label: "Today")
                Spacer()
            }
        }
        .padding(14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private func activityCell(day: DailyActivityDTO, isToday: Bool, maxCount: Int) -> some View {
        ZStack {
            RoundedRectangle(cornerRadius: 6)
                .fill(cellFill(count: day.count, hadDue: day.hadDue, isToday: isToday, maxCount: maxCount))

            if isToday {
                RoundedRectangle(cornerRadius: 6)
                    .stroke(Color.blue, lineWidth: 1.5)
            }

            if day.count > 0 {
                Text("\(day.count)")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(cellTextColor(count: day.count, isToday: isToday))
                    .monospacedDigit()
            }
        }
        .aspectRatio(1, contentMode: .fit)
    }

    private func cellFill(count: Int, hadDue: Bool, isToday: Bool, maxCount: Int) -> Color {
        if count > 0 {
            // 4 intensity steps mimic GitHub's heatmap
            let intensity = Double(count) / Double(maxCount)
            let opacity: Double
            switch intensity {
            case ..<0.25: opacity = 0.35
            case ..<0.5:  opacity = 0.55
            case ..<0.75: opacity = 0.75
            default:      opacity = 1.0
            }
            return .green.opacity(opacity)
        }
        if isToday { return .blue.opacity(0.12) }
        if hadDue  { return .red.opacity(0.30) }
        return .gray.opacity(0.18)
    }

    private func cellTextColor(count: Int, isToday: Bool) -> Color {
        // White on saturated green; on the lightest green keep contrast.
        .white
    }

    private func legendSwatch(color: Color, label: String) -> some View {
        HStack(spacing: 4) {
            RoundedRectangle(cornerRadius: 3)
                .fill(color)
                .frame(width: 10, height: 10)
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
    }

    private var emptyHint: some View {
        Text("No reviews yet. Once you start studying, your activity shows up here.")
            .font(.caption)
            .foregroundStyle(.secondary)
            .multilineTextAlignment(.center)
            .padding(.top, 4)
    }
}
