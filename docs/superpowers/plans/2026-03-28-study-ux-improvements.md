# Study UX Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the study experience across iOS and web: add deck selection to iOS dashboard (#43), show summary on early exit (#44), and widen card content display (#46).

**Architecture:** Three independent UI changes. #43 adds a deck list section to `DashboardView` backed by `DeckRepository`. #44 changes the X button behavior in `StudyView` to show the summary instead of dismissing. #46 widens CSS classes on web and reduces padding on iOS.

**Tech Stack:** SwiftUI (iOS), Vue 3 + Tailwind (web)

---

### Task 1: iOS — Add deck list to DashboardView (#43)

**Files:**
- Modify: `fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift`
- Modify: `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift`
- Modify: `fasolt.ios/Fasolt/Views/MainTabView.swift`

- [ ] **Step 1: Add DeckRepository dependency and deck state to DashboardViewModel**

In `fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift`, add a `DeckRepository` dependency and a `decks` property. Add a deck fetch to `loadStats()`.

Replace the current file content with:

```swift
import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Dashboard")

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
    var decks: [DeckDTO] = []

    private let apiClient: APIClient
    private let deckRepository: DeckRepository

    init(apiClient: APIClient, deckRepository: DeckRepository) {
        self.apiClient = apiClient
        self.deckRepository = deckRepository
    }

    func loadStats() async {
        isLoading = true
        errorMessage = nil

        let statsEndpoint = Endpoint(path: "/api/review/stats", method: .get)
        let overviewEndpoint = Endpoint(path: "/api/review/overview", method: .get)

        // Load independently so one failure doesn't block the other
        async let statsResult: Result<ReviewStatsDTO, Error> = {
            do { return .success(try await apiClient.request(statsEndpoint)) }
            catch { return .failure(error) }
        }()
        async let overviewResult: Result<OverviewDTO, Error> = {
            do { return .success(try await apiClient.request(overviewEndpoint)) }
            catch { return .failure(error) }
        }()
        async let decksResult: Result<[DeckDTO], Error> = {
            do { return .success(try await deckRepository.fetchDecks()) }
            catch { return .failure(error) }
        }()

        let (stats, overview, fetchedDecks) = await (statsResult, overviewResult, decksResult)

        var failed = false
        if case .success(let s) = stats {
            dueCount = s.dueCount
            totalCards = s.totalCards
            studiedToday = s.studiedToday
        } else { failed = true }

        if case .success(let o) = overview {
            cardsByState = o.cardsByState
            totalDecks = o.totalDecks
        } else { failed = true }

        if case .success(let d) = fetchedDecks {
            decks = d
        } else { failed = true }

        if failed {
            logger.error("Partial loadStats failure")
            errorMessage = "Some stats could not be loaded. Pull to refresh."
        }

        isLoading = false
    }
}
```

- [ ] **Step 2: Update MainTabView to pass DeckRepository to DashboardViewModel**

In `fasolt.ios/Fasolt/Views/MainTabView.swift`, change line 25 from:

```swift
                    DashboardView(
                        viewModel: DashboardViewModel(apiClient: authService.apiClient),
                        studyViewModelFactory: studyViewModelFactory
                    )
```

to:

```swift
                    DashboardView(
                        viewModel: DashboardViewModel(apiClient: authService.apiClient, deckRepository: deckRepository),
                        studyViewModelFactory: studyViewModelFactory
                    )
```

- [ ] **Step 3: Add deck list section and deck-scoped study navigation to DashboardView**

In `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift`, add a computed property and a new section. First, add a `@State` for deck-scoped study and a computed property for active decks with due cards. Then add the deck list section to the VStack.

Add after the `@State private var showStudy = false` line (line 70):

```swift
    @State private var selectedDeckId: String?
    @State private var showDeckStudy = false

    private var dueDecks: [DeckDTO] {
        viewModel.decks.filter { $0.isActive && $0.dueCount > 0 }
    }
```

Add after the `stateBar` section closing brace (after the `if viewModel.totalCards > 0` block, around line 21), inside the VStack:

```swift
                    if !dueDecks.isEmpty {
                        deckSection
                    }
```

Add the `deckSection` computed property before the `stateSegment` function (before line 165):

```swift
    private var deckSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Study by deck")
                .font(.caption2)
                .textCase(.uppercase)
                .tracking(1)
                .foregroundStyle(.secondary)

            ForEach(dueDecks, id: \.id) { deck in
                Button {
                    selectedDeckId = deck.id
                    showDeckStudy = true
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
        .navigationDestination(isPresented: $showDeckStudy) {
            StudyView(viewModel: studyViewModelFactory(), deckId: selectedDeckId)
        }
    }
```

- [ ] **Step 4: Build and verify**

Run: `cd fasolt.ios && xcodebuild build -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet 2>&1 | tail -5`

Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift fasolt.ios/Fasolt/Views/MainTabView.swift
git commit -m "feat(ios): add deck selection to dashboard (#43)"
```

---

### Task 2: iOS — Show summary on early exit (#44)

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/StudyView.swift`

- [ ] **Step 1: Change X button to show summary instead of dismissing**

In `fasolt.ios/Fasolt/Views/Study/StudyView.swift`, replace the exit confirmation alert and the X button action. The current behavior (lines 36-41) shows a confirmation dialog then dismisses. Change it to go directly to summary.

Replace the X button action (lines 36-41):

```swift
                    Button {
                        if (viewModel.cardsStudied > 0 || viewModel.skippedCount > 0) && viewModel.state != .summary {
                            showExitConfirmation = true
                        } else {
                            dismiss()
                        }
                    } label: {
```

with:

```swift
                    Button {
                        if (viewModel.cardsStudied > 0 || viewModel.skippedCount > 0) && viewModel.state != .summary {
                            viewModel.state = .summary
                        } else {
                            dismiss()
                        }
                    } label: {
```

Then remove the `.alert("End Session?"...` modifier (lines 66-71) entirely:

```swift
        .alert("End Session?", isPresented: $showExitConfirmation) {
            Button("Keep Studying", role: .cancel) {}
            Button("End", role: .destructive) { dismiss() }
        } message: {
            Text("You've studied \(viewModel.cardsStudied) of \(viewModel.totalCards) cards.")
        }
```

And remove the `@State private var showExitConfirmation = false` property on line 7.

- [ ] **Step 2: Build and verify**

Run: `cd fasolt.ios && xcodebuild build -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet 2>&1 | tail -5`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Study/StudyView.swift
git commit -m "feat(ios): show summary screen on early study exit (#44)"
```

---

### Task 3: Web — Widen card content display (#46)

**Files:**
- Modify: `fasolt.client/src/views/StudyView.vue`
- Modify: `fasolt.client/src/components/ReviewCard.vue`

- [ ] **Step 1: Widen StudyView container**

In `fasolt.client/src/views/StudyView.vue` line 32, change:

```html
  <div class="mx-auto max-w-[480px] space-y-6 py-8">
```

to:

```html
  <div class="mx-auto max-w-2xl space-y-6 py-8">
```

- [ ] **Step 2: Widen ReviewCard content areas**

In `fasolt.client/src/components/ReviewCard.vue`, replace all instances of `max-w-lg` with `max-w-2xl`. There are 4 occurrences on lines 32, 36, 41, and 44:

Line 32: `<div v-if="card.frontSvg" class="mt-4 flex w-full max-w-lg justify-center">`
Line 36: `class="mt-4 w-full max-w-lg text-center"`
Line 41: `<div v-if="isFlipped && card.backSvg" class="mt-4 flex w-full max-w-lg justify-center">`
Line 44: `<div v-if="isFlipped" class="mt-5 w-full max-w-lg text-center">`

Change each `max-w-lg` to `max-w-2xl`.

- [ ] **Step 3: Verify dev server renders correctly**

Run: `cd fasolt.client && npx vue-tsc --noEmit 2>&1 | tail -5`

Expected: No type errors.

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/StudyView.vue fasolt.client/src/components/ReviewCard.vue
git commit -m "feat(web): widen card content display in study view (#46)"
```

---

### Task 4: iOS — Reduce card padding (#46)

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/CardView.swift`

- [ ] **Step 1: Change uniform padding to directional padding**

In `fasolt.ios/Fasolt/Views/Study/CardView.swift` line 53, replace:

```swift
        .padding(24)
```

with:

```swift
        .padding(.horizontal, 16)
        .padding(.vertical, 24)
```

- [ ] **Step 2: Build and verify**

Run: `cd fasolt.ios && xcodebuild build -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet 2>&1 | tail -5`

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Study/CardView.swift
git commit -m "feat(ios): reduce horizontal card padding for wider content (#46)"
```

---

### Task 5: Playwright testing

**Files:** None (browser testing only)

- [ ] **Step 1: Start the full stack**

Ensure `./dev.sh` is running (backend + frontend + Postgres).

- [ ] **Step 2: Test web study flow with wider cards**

Using Playwright:
1. Navigate to the app and log in as `dev@fasolt.local` / `Dev1234!`
2. Navigate to `/study` — verify the study page renders with the wider container
3. Start a review session — verify card content uses the wider width
4. Complete or navigate back — verify no layout issues

- [ ] **Step 3: Test web deck-scoped study**

Using Playwright:
1. Navigate to `/study` — verify deck list shows with due badges
2. Click a deck with due cards — verify review starts with `?deckId=` in URL
3. Complete the session — verify summary shows correctly
