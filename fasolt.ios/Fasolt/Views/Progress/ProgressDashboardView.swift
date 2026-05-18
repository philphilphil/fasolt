import SwiftUI

struct ProgressDashboardView: View {
    @State private var viewModel: ProgressViewModel
    @State private var selectedRange: Range = .year

    enum Range: String, CaseIterable { case year = "Year", d90 = "90d", d30 = "30d", d7 = "7d"
        var days: Int {
            switch self {
            case .year: return 364
            case .d90: return 90
            case .d30: return 30
            case .d7: return 7
            }
        }
    }

    init(viewModel: ProgressViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 14) {
                    rangePicker
                    if let progress = viewModel.progress {
                        streakBanner(progress)
                        statTiles(progress)
                        activityCard(progress)
                        if let mix = progress.ratingMix, mix.total > 0 {
                            ratingMixCard(mix)
                        }
                        if progress.totalAnswered == 0 {
                            emptyHint
                        }
                    }
                }
                .padding(.horizontal, FasoltTheme.pagePadding)
                .padding(.bottom, 24)
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .scrollContentBackground(.hidden)
            .toolbar(.hidden, for: .navigationBar)
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
                        Button("Retry") { Task { await viewModel.load(days: selectedRange.days) } }
                    }
                }
            }
            .task { await viewModel.load(days: selectedRange.days) }
            .onChange(of: selectedRange) { _, newValue in
                Task { await viewModel.load(days: newValue.days) }
            }
            .onReceive(NotificationCenter.default.publisher(for: .appDidBecomeActive)) { _ in
                Task { await viewModel.load(days: selectedRange.days) }
            }
            .onReceive(NotificationCenter.default.publisher(for: .studySessionEnded)) { _ in
                Task { await viewModel.load(days: selectedRange.days) }
            }
            .offlineBanner()
        }
    }

    // MARK: - Range picker — refetches windowed data on change

    private var rangePicker: some View {
        Picker("Range", selection: $selectedRange) {
            ForEach(Range.allCases, id: \.self) { range in
                Text(range.rawValue).tag(range)
            }
        }
        .pickerStyle(.segmented)
        .padding(.top, 8)
    }

    // MARK: - Streak banner

    private func streakBanner(_ p: ProgressDTO) -> some View {
        ZStack(alignment: .top) {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .fill(FasoltTheme.paper1)
                .overlay(
                    RoundedRectangle(cornerRadius: 18, style: .continuous)
                        .strokeBorder(FasoltTheme.rule2, lineWidth: FasoltTheme.hairline)
                )

            VStack(spacing: 0) {
                AccentStripe(horizontalInset: 22)
                HStack(alignment: .bottom) {
                    VStack(alignment: .leading, spacing: 4) {
                        CapsLabel(text: p.currentStreak > 0 ? "Streak · active" : "Streak", color: FasoltTheme.accent)
                        HStack(alignment: .firstTextBaseline, spacing: 8) {
                            Text("\(p.currentStreak)")
                                .font(.system(size: 40, weight: .semibold))
                                .monospacedDigit()
                                .foregroundStyle(FasoltTheme.ink0)
                            Text("days · best \(p.bestStreak)")
                                .font(.system(size: 13))
                                .foregroundStyle(FasoltTheme.ink2)
                        }
                    }
                    Spacer()
                    miniStreakBars(p)
                }
                .padding(.horizontal, 18)
                .padding(.top, 12)
                .padding(.bottom, 14)
            }
        }
    }

    private func miniStreakBars(_ p: ProgressDTO) -> some View {
        // Show the last 14 days
        let last = Array(p.dailyActivity.suffix(14))
        let maxCount = max(1, last.map { $0.count }.max() ?? 1)
        return HStack(alignment: .bottom, spacing: 3) {
            ForEach(Array(last.enumerated()), id: \.offset) { i, day in
                let intensity = Double(day.count) / Double(maxCount)
                let h = 8 + CGFloat(intensity) * 28
                let isLast = i == last.count - 1
                RoundedRectangle(cornerRadius: 1.5)
                    .fill(isLast && day.count > 0
                          ? FasoltTheme.accent
                          : (day.count == 0 ? FasoltTheme.rule1 : FasoltTheme.good.opacity(0.3 + intensity * 0.6)))
                    .frame(width: 7, height: h)
            }
        }
        .frame(height: 36)
    }

    // MARK: - Stat tiles (single row, windowed)

    private func statTiles(_ p: ProgressDTO) -> some View {
        let windowAnswered = p.dailyActivity.map(\.count).reduce(0, +)
        let activeDays = p.dailyActivity.filter { $0.count > 0 }.count
        return HStack(spacing: 10) {
            statTile(
                label: "Answered",
                value: "\(windowAnswered)",
                sub: "in \(windowLabelShort)"
            )
            statTile(
                label: "Active days",
                value: "\(activeDays)",
                sub: "of \(p.dailyActivity.count) days"
            )
        }
    }

    private var windowLabelShort: String {
        switch selectedRange {
        case .year: return "this year"
        case .d90:  return "90 days"
        case .d30:  return "30 days"
        case .d7:   return "7 days"
        }
    }

    private func statTile(label: String, value: String, sub: String) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            CapsLabel(text: label, size: 10)
            Text(value)
                .font(.system(size: 22, weight: .semibold))
                .monospacedDigit()
                .foregroundStyle(FasoltTheme.ink0)
                .padding(.top, 6)
            Text(sub)
                .font(.system(size: 11))
                .foregroundStyle(FasoltTheme.ink2)
                .padding(.top, 4)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
        .paperCard(radius: 14)
    }

    // MARK: - Activity card

    private func activityCard(_ p: ProgressDTO) -> some View {
        let data = p.dailyActivity

        return VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    CapsLabel(text: titleForRange(), size: 11)
                    Text(headingForRange())
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(FasoltTheme.ink0)
                }
                Spacer()
                if useHeatmap {
                    legend()
                }
            }
            .padding(.bottom, 14)

            if useHeatmap {
                heatmap(data)
            } else {
                barChart(data)
            }

            HStack {
                heatmapStat(label: "Best", value: "\(data.map(\.count).max() ?? 0)")
                Spacer()
                heatmapStat(label: "Avg",  value: "\(averagePerDay(data))")
                Spacer()
                heatmapStat(label: "Rest", value: "\(data.filter { $0.count == 0 }.count)")
            }
            .padding(.top, 10)
        }
        .padding(16)
        .paperCard(radius: 18)
    }

    private var useHeatmap: Bool {
        selectedRange == .year || selectedRange == .d90
    }

    private func titleForRange() -> String {
        switch selectedRange {
        case .year: return "Year"
        case .d90: return "90 days"
        case .d30: return "30 days"
        case .d7: return "7 days"
        }
    }

    private func headingForRange() -> String {
        switch selectedRange {
        case .year: return "One year of practice"
        case .d90: return "Last 90 days"
        case .d30: return "Last 30 days"
        case .d7: return "This week"
        }
    }

    private func legend() -> some View {
        HStack(spacing: 6) {
            CapsLabel(text: "less", size: 9)
            HStack(spacing: 2) {
                ForEach(0..<5, id: \.self) { i in
                    RoundedRectangle(cornerRadius: 1.5)
                        .fill(cellColor(intensity: Double(i) / 4.0))
                        .frame(width: 7, height: 7)
                }
            }
            CapsLabel(text: "more", size: 9)
        }
    }

    private func heatmapStat(label: String, value: String) -> some View {
        HStack(spacing: 4) {
            Text(label)
                .font(.system(size: 11))
                .foregroundStyle(FasoltTheme.ink2)
            Text("·")
                .font(.system(size: 11))
                .foregroundStyle(FasoltTheme.ink3)
            Text(value)
                .font(.system(size: 11, weight: .medium))
                .monospacedDigit()
                .foregroundStyle(FasoltTheme.ink0)
        }
    }

    // MARK: - Heatmap (year / 90d)

    private func heatmap(_ data: [DailyActivityDTO]) -> some View {
        let maxCount = max(1, data.map { $0.count }.max() ?? 1)
        let cells = buildCells(data)
        let totalWeeks = (cells.count + 6) / 7
        let rowsOfWeeks = heatmapWeekRows  // 1 for 90d, 2 for year
        let weeksPerRow = (totalWeeks + rowsOfWeeks - 1) / rowsOfWeeks
        let rows = 7
        let gap: CGFloat = 2
        let rowGap: CGFloat = 8

        return GeometryReader { geo in
            let totalRows = rowsOfWeeks * rows
            let totalRowGap = CGFloat(rowsOfWeeks - 1) * rowGap
            let cellByWidth  = (geo.size.width  - CGFloat(weeksPerRow - 1) * gap) / CGFloat(weeksPerRow)
            let cellByHeight = (geo.size.height - CGFloat(totalRows - rowsOfWeeks) * gap - totalRowGap) / CGFloat(totalRows)
            let cell = max(0, min(cellByWidth, cellByHeight))

            VStack(spacing: rowGap) {
                ForEach(0..<rowsOfWeeks, id: \.self) { rowIdx in
                    let weekStart = rowIdx * weeksPerRow
                    let weekEnd = min(weekStart + weeksPerRow, totalWeeks)
                    HStack(spacing: gap) {
                        ForEach(weekStart..<weekEnd, id: \.self) { w in
                            VStack(spacing: gap) {
                                ForEach(0..<rows, id: \.self) { d in
                                    let idx = w * rows + d
                                    if idx < cells.count {
                                        cellView(cell: cells[idx], maxCount: maxCount)
                                            .frame(width: cell, height: cell)
                                    } else {
                                        Color.clear.frame(width: cell, height: cell)
                                    }
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
        .frame(height: heatmapHeight())
    }

    private var heatmapWeekRows: Int {
        selectedRange == .year ? 2 : 1
    }

    private func heatmapHeight() -> CGFloat {
        switch selectedRange {
        case .year: return 200   // 2 rows × 7 cells + row gap
        case .d90:  return 84
        case .d30, .d7: return 0
        }
    }

    // MARK: - Bar chart (30d / 7d)

    private func barChart(_ data: [DailyActivityDTO]) -> some View {
        let maxCount = max(1, data.map(\.count).max() ?? 1)
        let height: CGFloat = selectedRange == .d7 ? 140 : 110

        return GeometryReader { geo in
            let count = max(1, data.count)
            let gap: CGFloat = selectedRange == .d7 ? 6 : 3
            let barWidth = max(2, (geo.size.width - CGFloat(count - 1) * gap) / CGFloat(count))

            HStack(alignment: .bottom, spacing: gap) {
                ForEach(Array(data.enumerated()), id: \.offset) { i, day in
                    let intensity = Double(day.count) / Double(maxCount)
                    let isLast = i == data.count - 1
                    let barHeight = max(2, CGFloat(intensity) * (height - 4))
                    RoundedRectangle(cornerRadius: 2, style: .continuous)
                        .fill(
                            day.count == 0
                                ? FasoltTheme.paper2
                                : (isLast ? FasoltTheme.accent : cellColor(intensity: intensity))
                        )
                        .frame(width: barWidth, height: barHeight)
                }
            }
            .frame(width: geo.size.width, height: height, alignment: .bottomLeading)
        }
        .frame(height: height)
    }

    private struct Cell {
        let day: DailyActivityDTO?
        let isToday: Bool
    }

    private func buildCells(_ activity: [DailyActivityDTO]) -> [Cell] {
        let parser = DateFormatter()
        parser.dateFormat = "yyyy-MM-dd"
        parser.locale = Locale(identifier: "en_US_POSIX")

        guard let first = activity.first,
              let firstDate = parser.date(from: first.date) else {
            return activity.enumerated().map { i, d in
                Cell(day: d, isToday: i == activity.count - 1)
            }
        }

        var calendar = Calendar(identifier: .gregorian)
        calendar.firstWeekday = 2 // Monday
        let weekday = calendar.component(.weekday, from: firstDate)
        let leadingEmpty = (weekday - calendar.firstWeekday + 7) % 7

        var cells: [Cell] = []
        for _ in 0..<leadingEmpty { cells.append(Cell(day: nil, isToday: false)) }
        let lastIdx = activity.count - 1
        for (i, day) in activity.enumerated() {
            cells.append(Cell(day: day, isToday: i == lastIdx))
        }
        return cells
    }

    @ViewBuilder
    private func cellView(cell: Cell, maxCount: Int) -> some View {
        if let day = cell.day {
            let intensity = Double(day.count) / Double(maxCount)
            RoundedRectangle(cornerRadius: 1.5)
                .fill(day.count > 0 ? cellColor(intensity: intensity) : (day.hadDue ? FasoltTheme.again.opacity(0.18) : FasoltTheme.paper2))
                .overlay(
                    RoundedRectangle(cornerRadius: 1.5)
                        .strokeBorder(FasoltTheme.ink0, lineWidth: cell.isToday ? 1.2 : 0)
                )
        } else {
            Color.clear
        }
    }

    private func cellColor(intensity: Double) -> Color {
        if intensity <= 0 { return FasoltTheme.paper2 }
        if intensity < 0.25 { return Color(red: 0.93, green: 0.72, blue: 0.55) }
        if intensity < 0.5 { return Color(red: 0.88, green: 0.55, blue: 0.36) }
        if intensity < 0.75 { return Color(red: 0.80, green: 0.40, blue: 0.22) }
        return FasoltTheme.accent
    }

    private func averagePerDay(_ data: [DailyActivityDTO]) -> Int {
        let nonZero = data.filter { $0.count > 0 }
        guard !nonZero.isEmpty else { return 0 }
        let sum = nonZero.map(\.count).reduce(0, +)
        return sum / nonZero.count
    }

    // MARK: - Rating mix

    private func ratingMixCard(_ mix: RatingMixDTO) -> some View {
        let total = mix.total
        let entries: [(label: String, n: Int, color: Color)] = [
            ("Again", mix.again, FasoltTheme.again),
            ("Hard",  mix.hard,  FasoltTheme.hard),
            ("Good",  mix.good,  FasoltTheme.good),
            ("Easy",  mix.easy,  FasoltTheme.easy),
        ]

        return VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    CapsLabel(text: "Rating mix", size: 11)
                    Text("How you rate yourself")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(FasoltTheme.ink0)
                }
                Spacer()
                Text("\(total)")
                    .font(.system(size: 11, weight: .medium, design: .monospaced))
                    .foregroundStyle(FasoltTheme.ink2)
                    + Text(" ratings")
                    .font(.system(size: 11))
                    .foregroundStyle(FasoltTheme.ink2)
            }
            .padding(.bottom, 14)

            // Proportional stacked bar
            GeometryReader { geo in
                HStack(spacing: 0) {
                    ForEach(entries.indices, id: \.self) { i in
                        let e = entries[i]
                        let frac = total > 0 ? CGFloat(e.n) / CGFloat(total) : 0
                        Rectangle()
                            .fill(e.color)
                            .frame(width: max(0, frac * geo.size.width))
                    }
                }
            }
            .frame(height: 10)
            .clipShape(RoundedRectangle(cornerRadius: 3, style: .continuous))

            // 4-up breakdown
            HStack(alignment: .top, spacing: 10) {
                ForEach(entries.indices, id: \.self) { i in
                    let e = entries[i]
                    let pct = total > 0 ? Int((Double(e.n) / Double(total)) * 100.0) : 0
                    VStack(alignment: .leading, spacing: 4) {
                        HStack(spacing: 5) {
                            RoundedRectangle(cornerRadius: 2)
                                .fill(e.color)
                                .frame(width: 6, height: 6)
                            CapsLabel(text: e.label, color: e.color, size: 10)
                        }
                        Text("\(e.n)")
                            .font(.system(size: 20, weight: .semibold))
                            .monospacedDigit()
                            .foregroundStyle(FasoltTheme.ink0)
                        Text("\(pct)%")
                            .font(.system(size: 11, design: .monospaced))
                            .foregroundStyle(FasoltTheme.ink3)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding(.top, 14)

            Text("Honest ratings help FSRS schedule better.")
                .font(.system(size: 11))
                .foregroundStyle(FasoltTheme.ink2)
                .padding(.top, 12)
        }
        .padding(16)
        .paperCard(radius: 18)
    }

    // MARK: - Empty hint

    private var emptyHint: some View {
        Text("No reviews yet. Once you start studying, your activity shows up here.")
            .font(.system(size: 13))
            .foregroundStyle(FasoltTheme.ink2)
            .multilineTextAlignment(.center)
            .padding(.top, 8)
    }
}
