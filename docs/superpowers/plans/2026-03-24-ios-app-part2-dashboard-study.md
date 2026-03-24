# iOS App Part 2: Dashboard + Study Session — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the core study loop — a motivational dashboard with stats and a study session with card flip, FSRS rating, and offline review queuing.

**Architecture:** DashboardViewModel and StudyViewModel (`@Observable`) drive two screens. CardRepository coordinates between APIClient (network) and SwiftData (offline queue). Views follow the existing pattern from Part 1.

**Tech Stack:** SwiftUI, SwiftData, URLSession async/await, existing APIClient/NetworkMonitor from Part 1. iOS 17+, Swift 6.

---

## File Map

```
fasolt.ios/Fasolt/
├── ViewModels/
│   ├── DashboardViewModel.swift    — fetches review stats + overview
│   └── StudyViewModel.swift        — manages study session state machine
├── Views/
│   ├── Dashboard/
│   │   └── DashboardView.swift     — MODIFY: replace stub with real dashboard
│   └── Study/
│       ├── StudyView.swift         — MODIFY: replace stub with study session
│       ├── CardView.swift          — NEW: single card face (front or back)
│       └── StudySummaryView.swift  — NEW: session completion screen
├── Repositories/
│   └── CardRepository.swift        — NEW: network + offline queue coordination
```

**Existing files used (read-only context):**
- `Services/APIClient.swift` — `request<T>(_:)` for authenticated API calls
- `Services/Endpoint.swift` — `Endpoint` struct, `HTTPMethod`, `APIError`
- `Models/APIModels.swift` — `DueCardDTO`, `RateCardRequest`, `RateCardResponse`, `ReviewStatsDTO`, `OverviewDTO`
- `Models/PendingReview.swift` — SwiftData model for offline queue
- `Utilities/NetworkMonitor.swift` — `isConnected` property
- `FasoltApp.swift` — needs modification to inject dependencies
- `Views/MainTabView.swift` — needs modification for NavigationStack

---

### Task 1: CardRepository

**Files:**
- Create: `fasolt.ios/Fasolt/Repositories/CardRepository.swift`

- [ ] **Step 1: Create CardRepository**

Create `fasolt.ios/Fasolt/Repositories/CardRepository.swift`:

```swift
import Foundation
import SwiftData

@MainActor
@Observable
final class CardRepository {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext

    init(apiClient: APIClient, networkMonitor: NetworkMonitor, modelContext: ModelContext) {
        self.apiClient = apiClient
        self.networkMonitor = networkMonitor
        self.modelContext = modelContext
    }

    // MARK: - Fetch due cards

    func fetchDueCards(deckId: String? = nil, limit: Int = 50) async throws -> [DueCardDTO] {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let deckId {
            queryItems.append(URLQueryItem(name: "deckId", value: deckId))
        }
        let endpoint = Endpoint(path: "/api/review/due", method: .get, queryItems: queryItems)
        return try await apiClient.request(endpoint)
    }

    // MARK: - Rate a card

    func rateCard(cardId: String, rating: String) async throws -> RateCardResponse? {
        if networkMonitor.isConnected {
            do {
                let body = RateCardRequest(cardId: cardId, rating: rating)
                let endpoint = Endpoint(path: "/api/review/rate", method: .post, body: body)
                let response: RateCardResponse = try await apiClient.request(endpoint)
                return response
            } catch let error as APIError {
                switch error {
                case .networkError:
                    break // Fall through to offline queue
                default:
                    throw error // Non-network errors (401, 404, etc.) propagate
                }
            }
        }

        // Offline or network error — queue for later sync
        let pending = PendingReview(cardPublicId: cardId, rating: rating)
        modelContext.insert(pending)
        try modelContext.save()
        return nil
    }

    // MARK: - Flush pending reviews (stub for Part 4)

    func flushPendingReviews() async throws -> Int {
        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate { !$0.synced }
        )
        let pending = try modelContext.fetch(descriptor)
        return pending.count
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Repositories/CardRepository.swift
git commit -m "feat(ios): add CardRepository with offline review queue

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: DashboardViewModel

**Files:**
- Create: `fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift`

- [ ] **Step 1: Create DashboardViewModel**

Create `fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift`:

```swift
import Foundation

@MainActor
@Observable
final class DashboardViewModel {
    var dueCount: Int = 0
    var totalCards: Int = 0
    var studiedToday: Int = 0
    var cardsByState: [String: Int] = [:]
    var totalDecks: Int = 0
    var isLoading = false
    var errorMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadStats() async {
        isLoading = true
        errorMessage = nil

        do {
            async let statsResult: ReviewStatsDTO = {
                let endpoint = Endpoint(path: "/api/review/stats", method: .get)
                return try await apiClient.request(endpoint)
            }()

            async let overviewResult: OverviewDTO = {
                let endpoint = Endpoint(path: "/api/overview", method: .get)
                return try await apiClient.request(endpoint)
            }()

            let stats = try await statsResult
            let overview = try await overviewResult

            dueCount = stats.dueCount
            totalCards = stats.totalCards
            studiedToday = stats.studiedToday
            cardsByState = overview.cardsByState
            totalDecks = overview.totalDecks
        } catch {
            errorMessage = "Could not load stats. Pull to refresh."
        }

        isLoading = false
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift
git commit -m "feat(ios): add DashboardViewModel with stats loading

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: StudyViewModel

**Files:**
- Create: `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`

- [ ] **Step 1: Create StudyViewModel**

Create `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`:

```swift
import Foundation

@MainActor
@Observable
final class StudyViewModel {
    enum SessionState {
        case idle, loading, studying, flipped, summary
    }

    // State
    var state: SessionState = .idle
    var errorMessage: String?

    // Session data
    var cards: [DueCardDTO] = []
    var currentIndex: Int = 0
    var isFlipped: Bool = false

    // Session stats
    var ratingsCount: [String: Int] = ["again": 0, "hard": 0, "good": 0, "easy": 0]
    var cardsStudied: Int = 0

    private let cardRepository: CardRepository

    init(cardRepository: CardRepository) {
        self.cardRepository = cardRepository
    }

    // MARK: - Computed

    var currentCard: DueCardDTO? {
        guard currentIndex < cards.count else { return nil }
        return cards[currentIndex]
    }

    var progress: Double {
        guard !cards.isEmpty else { return 0 }
        return Double(currentIndex) / Double(cards.count)
    }

    var totalCards: Int {
        cards.count
    }

    // MARK: - Actions

    func startSession(deckId: String? = nil) async {
        state = .loading
        errorMessage = nil

        do {
            cards = try await cardRepository.fetchDueCards(deckId: deckId)
            if cards.isEmpty {
                state = .summary
            } else {
                currentIndex = 0
                isFlipped = false
                cardsStudied = 0
                ratingsCount = ["again": 0, "hard": 0, "good": 0, "easy": 0]
                state = .studying
            }
        } catch {
            errorMessage = "Could not load cards. Check your connection."
            state = .idle
        }
    }

    func flipCard() {
        isFlipped = true
        state = .flipped
    }

    func rateCard(_ rating: String) async {
        guard let card = currentCard else { return }

        do {
            _ = try await cardRepository.rateCard(cardId: card.id, rating: rating)
        } catch {
            // Non-network error — show briefly but continue session
            // The card is still removed from the session
        }

        ratingsCount[rating, default: 0] += 1
        cardsStudied += 1
        currentIndex += 1
        isFlipped = false

        if currentIndex >= cards.count {
            state = .summary
        } else {
            state = .studying
        }
    }

    func exitSession() {
        state = .idle
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift
git commit -m "feat(ios): add StudyViewModel with session state machine

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: CardView + StudySummaryView

**Files:**
- Create: `fasolt.ios/Fasolt/Views/Study/CardView.swift`
- Create: `fasolt.ios/Fasolt/Views/Study/StudySummaryView.swift`

- [ ] **Step 1: Create CardView**

Create `fasolt.ios/Fasolt/Views/Study/CardView.swift`:

```swift
import SwiftUI

struct CardView: View {
    let label: String
    let text: String
    let sourceFile: String?
    let sourceHeading: String?

    var body: some View {
        VStack(spacing: 0) {
            Spacer()

            Text(label)
                .font(.caption2)
                .textCase(.uppercase)
                .tracking(1)
                .foregroundStyle(.secondary)
                .padding(.bottom, 12)

            Text(text)
                .font(.title3)
                .multilineTextAlignment(.center)
                .foregroundStyle(.primary)
                .padding(.horizontal, 8)

            Spacer()

            if let sourceFile {
                HStack(spacing: 4) {
                    Text(sourceFile)
                    if let sourceHeading {
                        Text("·")
                        Text(sourceHeading)
                    }
                }
                .font(.caption2)
                .foregroundStyle(.tertiary)
                .padding(.bottom, 4)
            }
        }
        .frame(maxWidth: .infinity)
        .padding(24)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
        .overlay(
            RoundedRectangle(cornerRadius: 16)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
    }
}

#Preview("Question") {
    CardView(
        label: "Question",
        text: "What organelle is responsible for producing ATP?",
        sourceFile: "biology-101.md",
        sourceHeading: "Cell Structure"
    )
    .padding()
    .background(.black)
}

#Preview("Answer") {
    CardView(
        label: "Answer",
        text: "The mitochondria is the powerhouse of the cell.",
        sourceFile: "biology-101.md",
        sourceHeading: nil
    )
    .padding()
    .background(.black)
}
```

- [ ] **Step 2: Create StudySummaryView**

Create `fasolt.ios/Fasolt/Views/Study/StudySummaryView.swift`:

```swift
import SwiftUI

struct StudySummaryView: View {
    let cardsStudied: Int
    let ratingsCount: [String: Int]
    let onDone: () -> Void

    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 56))
                .foregroundStyle(.green)

            Text("Session Complete")
                .font(.title2.bold())

            // Stats card
            VStack(spacing: 12) {
                HStack {
                    Text("Cards studied")
                        .foregroundStyle(.secondary)
                    Spacer()
                    Text("\(cardsStudied)")
                        .fontWeight(.semibold)
                }

                Divider()

                ratingRow("Again", count: ratingsCount["again"] ?? 0, color: .red)
                ratingRow("Hard", count: ratingsCount["hard"] ?? 0, color: .orange)
                ratingRow("Good", count: ratingsCount["good"] ?? 0, color: .green)
                ratingRow("Easy", count: ratingsCount["easy"] ?? 0, color: .blue)
            }
            .padding()
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))

            Spacer()

            Button("Done") {
                onDone()
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .frame(maxWidth: .infinity)
        }
        .padding()
    }

    private func ratingRow(_ label: String, count: Int, color: Color) -> some View {
        HStack {
            Circle()
                .fill(color)
                .frame(width: 8, height: 8)
            Text(label)
                .foregroundStyle(.secondary)
            Spacer()
            Text("\(count)")
                .fontWeight(.medium)
        }
    }
}

#Preview {
    StudySummaryView(
        cardsStudied: 23,
        ratingsCount: ["again": 3, "hard": 5, "good": 12, "easy": 3],
        onDone: {}
    )
}
```

- [ ] **Step 3: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Study/CardView.swift fasolt.ios/Fasolt/Views/Study/StudySummaryView.swift
git commit -m "feat(ios): add CardView and StudySummaryView components

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: StudyView (replace stub)

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/StudyView.swift`

- [ ] **Step 1: Replace StudyView stub**

Replace the contents of `fasolt.ios/Fasolt/Views/Study/StudyView.swift` with:

```swift
import SwiftUI
import UIKit

struct StudyView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: StudyViewModel
    @State private var showExitConfirmation = false

    init(viewModel: StudyViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        Group {
            switch viewModel.state {
            case .idle, .loading:
                loadingView
            case .studying, .flipped:
                cardContent
            case .summary:
                StudySummaryView(
                    cardsStudied: viewModel.cardsStudied,
                    ratingsCount: viewModel.ratingsCount,
                    onDone: { dismiss() }
                )
            }
        }
        .navigationBarBackButtonHidden(true)
        .toolbar {
            ToolbarItem(placement: .topBarLeading) {
                if viewModel.state != .summary {
                    Button {
                        if viewModel.cardsStudied > 0 && viewModel.state != .summary {
                            showExitConfirmation = true
                        } else {
                            dismiss()
                        }
                    } label: {
                        Image(systemName: "xmark")
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .alert("End Session?", isPresented: $showExitConfirmation) {
            Button("Keep Studying", role: .cancel) {}
            Button("End", role: .destructive) { dismiss() }
        } message: {
            Text("You've studied \(viewModel.cardsStudied) of \(viewModel.totalCards) cards.")
        }
        .task {
            if viewModel.state == .idle {
                await viewModel.startSession()
            }
        }
    }

    // MARK: - Loading

    private var loadingView: some View {
        VStack(spacing: 16) {
            if let error = viewModel.errorMessage {
                Text(error)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                Button("Retry") {
                    Task { await viewModel.startSession() }
                }
                .buttonStyle(.bordered)
            } else {
                ProgressView("Loading cards...")
            }
        }
        .padding()
    }

    // MARK: - Card Content (front or back depending on flip state)

    private var cardContent: some View {
        VStack(spacing: 0) {
            progressBar
                .padding(.horizontal)
                .padding(.top, 8)

            Spacer()

            if let card = viewModel.currentCard {
                CardView(
                    label: viewModel.isFlipped ? "Answer" : "Question",
                    text: viewModel.isFlipped ? card.back : card.front,
                    sourceFile: card.sourceFile,
                    sourceHeading: viewModel.isFlipped ? card.sourceHeading : nil
                )
                .padding(.horizontal)
                .rotation3DEffect(
                    .degrees(viewModel.isFlipped ? 180 : 0),
                    axis: (x: 0, y: 1, z: 0),
                    perspective: 0.5
                )
                .onTapGesture {
                    if !viewModel.isFlipped {
                        flipWithHaptic()
                    }
                }
            }

            Spacer()

            if viewModel.isFlipped {
                ratingButtons
                    .padding()
                    .transition(.move(edge: .bottom).combined(with: .opacity))
            } else {
                Button {
                    flipWithHaptic()
                } label: {
                    Text("Show Answer")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .padding()
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
    }

    private func flipWithHaptic() {
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.impactOccurred()
        withAnimation(.spring(duration: 0.4)) {
            viewModel.flipCard()
        }
    }

    // MARK: - Progress Bar

    private var progressBar: some View {
        HStack(spacing: 8) {
            Text("\(viewModel.currentIndex + 1) / \(viewModel.totalCards)")
                .font(.caption)
                .foregroundStyle(.secondary)
                .monospacedDigit()

            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(.quaternary)
                    Capsule()
                        .fill(.blue)
                        .frame(width: geo.size.width * viewModel.progress)
                }
            }
            .frame(height: 4)
        }
    }

    // MARK: - Rating Buttons

    private var ratingButtons: some View {
        HStack(spacing: 8) {
            ratingButton("Again", color: .red, rating: "again")
            ratingButton("Hard", color: .orange, rating: "hard")
            ratingButton("Good", color: .green, rating: "good")
            ratingButton("Easy", color: .blue, rating: "easy")
        }
    }

    private func ratingButton(_ label: String, color: Color, rating: String) -> some View {
        Button {
            Task {
                await viewModel.rateCard(rating)
            }
        } label: {
            Text(label)
                .font(.subheadline.weight(.medium))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
        }
        .buttonStyle(.bordered)
        .tint(color)
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: BUILD SUCCEEDED (may have warnings about unused StudyView in MainTabView — that's fine, we'll fix in Task 7).

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Study/StudyView.swift
git commit -m "feat(ios): implement StudyView with flip and rating flow

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: DashboardView (replace stub)

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift`

- [ ] **Step 1: Replace DashboardView stub**

Replace the contents of `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift` with:

```swift
import SwiftUI

struct DashboardView: View {
    @State private var viewModel: DashboardViewModel
    private let studyViewModelFactory: () -> StudyViewModel

    init(viewModel: DashboardViewModel, studyViewModelFactory: @escaping () -> StudyViewModel) {
        _viewModel = State(initialValue: viewModel)
        self.studyViewModelFactory = studyViewModelFactory
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
                if let error = viewModel.errorMessage {
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
        }
    }

    // MARK: - Hero Card

    private var heroCard: some View {
        NavigationLink {
            StudyView(viewModel: studyViewModelFactory())
        } label: {
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
        }
        .disabled(viewModel.dueCount == 0)
    }

    // MARK: - Stats Row

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

    // MARK: - State Bar

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
```

- [ ] **Step 2: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: Build will fail because DashboardView now requires init parameters. This is expected — we fix in the next steps.

- [ ] **Step 3: Update FasoltApp to inject NetworkMonitor**

Replace the contents of `fasolt.ios/Fasolt/FasoltApp.swift` with:

```swift
import SwiftUI
import SwiftData

@main
struct FasoltApp: App {
    @State private var authService = AuthService()
    @State private var networkMonitor = NetworkMonitor()

    var body: some Scene {
        WindowGroup {
            Group {
                if authService.isAuthenticated {
                    MainTabView()
                } else {
                    OnboardingView()
                }
            }
            .animation(.default, value: authService.isAuthenticated)
        }
        .environment(authService)
        .environment(networkMonitor)
        .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
    }
}
```

- [ ] **Step 4: Update MainTabView to pass dependencies**

Replace the contents of `fasolt.ios/Fasolt/Views/MainTabView.swift` with:

```swift
import SwiftUI
import SwiftData

struct MainTabView: View {
    @Environment(AuthService.self) private var authService
    @Environment(NetworkMonitor.self) private var networkMonitor
    @Environment(\.modelContext) private var modelContext

    var body: some View {
        TabView {
            DashboardView(
                viewModel: DashboardViewModel(apiClient: authService.apiClient),
                studyViewModelFactory: {
                    StudyViewModel(
                        cardRepository: CardRepository(
                            apiClient: authService.apiClient,
                            networkMonitor: networkMonitor,
                            modelContext: modelContext
                        )
                    )
                }
            )
            .tabItem {
                Label("Dashboard", systemImage: "chart.bar")
            }

            DeckListView()
                .tabItem {
                    Label("Decks", systemImage: "rectangle.stack")
                }

            SettingsView()
                .tabItem {
                    Label("Settings", systemImage: "gear")
                }
        }
    }
}
```

- [ ] **Step 5: Build to verify compilation**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -5
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 6: Run all tests**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodebuild test -project Fasolt.xcodeproj -scheme FasoltTests -destination 'platform=iOS Simulator,name=iPhone 17 Pro' 2>&1 | tail -20
```

Expected: All existing tests pass.

- [ ] **Step 7: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift fasolt.ios/Fasolt/FasoltApp.swift fasolt.ios/Fasolt/Views/MainTabView.swift
git commit -m "feat(ios): implement DashboardView and wire up dependencies

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Manual Integration Test

**Precondition:** Backend must be running (`./dev.sh` from the repo root). Must have some cards created (use the MCP tools or web app to create test cards).

- [ ] **Step 1: Build and launch in Simulator**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios
xcodegen generate
xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build 2>&1 | tail -3
xcrun simctl install booted ~/Library/Developer/Xcode/DerivedData/Fasolt-*/Build/Products/Debug-iphonesimulator/Fasolt.app
xcrun simctl launch booted com.fasolt.app
```

- [ ] **Step 2: Test dashboard**

1. Sign in (should auto-sign-in if tokens from Part 1 persist)
2. Verify dashboard shows: hero card with due count, stats row (Total, Today, Decks), state bar
3. Pull to refresh — verify stats reload
4. If no due cards: verify "All caught up!" and Study Now is disabled

- [ ] **Step 3: Create test cards if needed**

If no cards exist, create some via the backend:

```bash
curl -X POST http://localhost:8080/api/cards/bulk \
  -H "Content-Type: application/json" \
  -H "Cookie: $(curl -s -c - http://localhost:8080/api/identity/login -H 'Content-Type: application/json' -d '{"email":"dev@fasolt.local","password":"Dev1234!"}' | grep '.AspNetCore' | awk '{print $6"="$7}')" \
  -d '{"cards":[{"front":"What is 2+2?","back":"4"},{"front":"Capital of France?","back":"Paris"},{"front":"What is HTTP?","back":"HyperText Transfer Protocol"}]}'
```

Then pull to refresh on the dashboard.

- [ ] **Step 4: Test study session**

1. Tap "Study Now" on the hero card
2. Verify: progress bar shows "1 / N", card shows question (front), "Show Answer" button visible
3. Tap card or "Show Answer" — verify card flips to answer (back), rating buttons appear
4. Tap "Good" — verify next card appears, progress bar updates
5. Rate all cards — verify summary screen shows with correct counts
6. Tap "Done" — verify return to dashboard, stats updated

- [ ] **Step 5: Test early exit**

1. Start a new study session
2. Rate 1-2 cards
3. Tap the X button
4. Verify confirmation dialog: "End Session? You've studied 2 of N cards."
5. Tap "End" — verify return to dashboard

- [ ] **Step 6: Test offline behavior**

1. Enable Airplane Mode in Simulator (or disconnect network)
2. Start a study session (should fail with error if cards aren't cached from a previous session)
3. If cards were already loaded: rate a card — should succeed silently (queued offline)
4. Reconnect — ratings should be pending for Part 4 sync

- [ ] **Step 7: Commit any fixes**

If any issues were found and fixed during testing, commit those fixes.
