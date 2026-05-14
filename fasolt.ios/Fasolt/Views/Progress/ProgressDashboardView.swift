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

    private static let cellSize: CGFloat = 26
    private static let cellSpacing: CGFloat = 4

    private func activityGrid(_ p: ProgressDTO) -> some View {
        let maxCount = max(1, p.dailyActivity.map { $0.count }.max() ?? 1)
        let cells = buildCells(p)
        let cols = Array(repeating: GridItem(.fixed(Self.cellSize), spacing: Self.cellSpacing), count: 7)
        let studiedDays = p.dailyActivity.filter { $0.count > 0 }.count

        return VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .firstTextBaseline) {
                Text("LAST 30 DAYS")
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .tracking(0.6)
                Spacer()
                Text("\(studiedDays) of \(p.dailyActivity.count) studied")
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .monospacedDigit()
            }

            HStack {
                Spacer(minLength: 0)
                LazyVGrid(columns: cols, alignment: .center, spacing: Self.cellSpacing) {
                    ForEach(cells) { cell in
                        if let day = cell.day {
                            activityCell(day: day, isToday: cell.isToday, maxCount: maxCount)
                        } else {
                            Color.clear.frame(width: Self.cellSize, height: Self.cellSize)
                        }
                    }
                }
                .fixedSize()
                Spacer(minLength: 0)
            }

            HStack(spacing: 14) {
                legendSwatch(color: studiedSwatchColor, label: "Studied")
                legendSwatch(color: missedColor, label: "Missed")
                legendSwatch(color: restColor, label: "Rest")
                HStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 3)
                        .stroke(Color.blue, lineWidth: 1.5)
                        .frame(width: 10, height: 10)
                    Text("Today")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
                Spacer()
            }
        }
        .padding(14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10))
    }

    private func activityCell(day: DailyActivityDTO, isToday: Bool, maxCount: Int) -> some View {
        ZStack {
            RoundedRectangle(cornerRadius: 5)
                .fill(cellFill(count: day.count, hadDue: day.hadDue, maxCount: maxCount))

            if isToday {
                RoundedRectangle(cornerRadius: 5)
                    .stroke(Color.blue, lineWidth: 1.5)
            }

            if day.count > 0 {
                Text("\(day.count)")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(cellTextColor(count: day.count, maxCount: maxCount))
                    .monospacedDigit()
            }
        }
        .frame(width: Self.cellSize, height: Self.cellSize)
    }

    // Cell-color palette: greens for studied (4 stepped intensities),
    // very subtle warm/neutral grays for missed/rest so the chart doesn't feel punishing.
    private var restColor: Color { Color.gray.opacity(0.14) }
    private var missedColor: Color { Color.orange.opacity(0.18) }
    private var studiedSwatchColor: Color { Color.green.opacity(0.7) }

    private func cellFill(count: Int, hadDue: Bool, maxCount: Int) -> Color {
        if count > 0 {
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
        return hadDue ? missedColor : restColor
    }

    private func cellTextColor(count: Int, maxCount: Int) -> Color {
        // Use white only when the green is dark enough; otherwise fall back to a darker green.
        let intensity = Double(count) / Double(maxCount)
        return intensity >= 0.5 ? .white : Color(red: 0.15, green: 0.45, blue: 0.20)
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

    // Build cells with leading placeholders so the grid aligns to weekdays (Mon-first).
    private struct ActivityCell: Identifiable {
        let id: String
        let day: DailyActivityDTO?
        let isToday: Bool
    }

    private static let dateParser: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        f.locale = Locale(identifier: "en_US_POSIX")
        f.timeZone = TimeZone.current
        return f
    }()

    private func buildCells(_ p: ProgressDTO) -> [ActivityCell] {
        guard let first = p.dailyActivity.first,
              let firstDate = Self.dateParser.date(from: first.date) else {
            return p.dailyActivity.enumerated().map { idx, day in
                ActivityCell(id: day.id, day: day, isToday: idx == p.dailyActivity.count - 1)
            }
        }

        var calendar = Calendar(identifier: .gregorian)
        calendar.firstWeekday = 2 // Monday
        let weekday = calendar.component(.weekday, from: firstDate) // Sun=1..Sat=7
        let leadingEmpty = (weekday - calendar.firstWeekday + 7) % 7

        var cells: [ActivityCell] = []
        for i in 0..<leadingEmpty {
            cells.append(ActivityCell(id: "pad-\(i)", day: nil, isToday: false))
        }
        let lastIdx = p.dailyActivity.count - 1
        for (idx, day) in p.dailyActivity.enumerated() {
            cells.append(ActivityCell(id: day.id, day: day, isToday: idx == lastIdx))
        }
        return cells
    }

    private var emptyHint: some View {
        Text("No reviews yet. Once you start studying, your activity shows up here.")
            .font(.caption)
            .foregroundStyle(.secondary)
            .multilineTextAlignment(.center)
            .padding(.top, 4)
    }
}
