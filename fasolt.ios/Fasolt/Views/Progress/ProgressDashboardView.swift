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
                        activityChart(progress)
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
        let cells: [(String, String, Color?)] = [
            ("CURRENT STREAK", "\(p.currentStreak == 0 ? "" : "🔥 ")\(p.currentStreak)", nil),
            ("BEST STREAK", "\(p.bestStreak)", nil),
            ("TOTAL ANSWERED", "\(p.totalAnswered)", nil),
            ("TODAY", "\(p.answeredToday)", nil),
        ]
        return LazyVGrid(columns: [GridItem(.flexible(), spacing: 10), GridItem(.flexible(), spacing: 10)], spacing: 10) {
            ForEach(cells, id: \.0) { cell in
                VStack(alignment: .leading, spacing: 4) {
                    Text(cell.0)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .tracking(0.6)
                    Text(cell.1)
                        .font(.title2.weight(.bold))
                        .monospacedDigit()
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(14)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
            }
        }
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

    private func activityChart(_ p: ProgressDTO) -> some View {
        let maxCount = max(1, p.dailyActivity.map { $0.count }.max() ?? 1)
        return VStack(alignment: .leading, spacing: 10) {
            Text("LAST 30 DAYS")
                .font(.caption2)
                .foregroundStyle(.secondary)
                .tracking(0.6)

            HStack(alignment: .bottom, spacing: 3) {
                ForEach(Array(p.dailyActivity.enumerated()), id: \.element.id) { idx, day in
                    let isToday = idx == p.dailyActivity.count - 1
                    RoundedRectangle(cornerRadius: 2)
                        .fill(barColor(day, isToday: isToday))
                        .frame(height: barHeight(day.count, max: maxCount))
                        .frame(maxWidth: .infinity)
                }
            }
            .frame(height: 64)

            HStack(spacing: 12) {
                legendDot(color: .green, label: "Studied")
                legendDot(color: .red.opacity(0.4), label: "Missed")
                legendDot(color: .gray.opacity(0.4), label: "Rest")
                legendDot(color: .blue, label: "Today")
                Spacer()
            }
        }
        .padding(14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private func barColor(_ day: DailyActivityDTO, isToday: Bool) -> Color {
        if isToday { return .blue }
        if day.count > 0 { return .green }
        if day.hadDue { return .red.opacity(0.4) }
        return .gray.opacity(0.25)
    }

    private func barHeight(_ count: Int, max: Int) -> CGFloat {
        if count == 0 { return 6 }
        let minH: CGFloat = 10
        let maxH: CGFloat = 56
        let pct = CGFloat(count) / CGFloat(max)
        return minH + pct * (maxH - minH)
    }

    private func legendDot(color: Color, label: String) -> some View {
        HStack(spacing: 4) {
            RoundedRectangle(cornerRadius: 2)
                .fill(color)
                .frame(width: 8, height: 8)
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
