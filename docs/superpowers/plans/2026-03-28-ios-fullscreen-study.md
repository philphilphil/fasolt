# iOS Full-Screen Study View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Present the study view as a full-screen modal that hides the tab bar, making study sessions immersive and distraction-free.

**Architecture:** A custom `EnvironmentValues` key (`startStudy`) lets any child view trigger a study session. `MainTabView` holds the state and presents the modal via `.fullScreenCover`. All three existing entry points (dashboard hero, dashboard deck row, deck detail button) are rewired to use this environment action instead of `NavigationLink`/`navigationDestination`.

**Tech Stack:** SwiftUI, iOS 17+

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `fasolt.ios/Fasolt/Views/Study/StartStudyAction.swift` | Create | Environment key + action type for triggering study |
| `fasolt.ios/Fasolt/Views/MainTabView.swift` | Modify | Add modal state, `.fullScreenCover`, environment provider |
| `fasolt.ios/Fasolt/Views/Study/StudyView.swift` | Modify | Swap toolbar positions (X to trailing, pause/skip to leading), remove `navigationBarBackButtonHidden` |
| `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift` | Modify | Remove factory param, remove navigation destinations, use `startStudy` environment |
| `fasolt.ios/Fasolt/Views/Decks/DeckListView.swift` | Modify | Remove factory param, stop passing it to `DeckDetailView` |
| `fasolt.ios/Fasolt/Views/Decks/DeckDetailView.swift` | Modify | Remove factory param, replace `NavigationLink` with `Button` using `startStudy` environment |

---

### Task 1: Create StartStudyAction Environment Key

**Files:**
- Create: `fasolt.ios/Fasolt/Views/Study/StartStudyAction.swift`

- [ ] **Step 1: Create the environment key file**

```swift
import SwiftUI

struct StartStudyAction {
    let action: (String?) -> Void
    func callAsFunction(deckId: String? = nil) { action(deckId) }
}

struct StartStudyKey: EnvironmentKey {
    static let defaultValue = StartStudyAction { _ in }
}

extension EnvironmentValues {
    var startStudy: StartStudyAction {
        get { self[StartStudyKey.self] }
        set { self[StartStudyKey.self] = newValue }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Study/StartStudyAction.swift
git commit -m "feat(ios): add StartStudyAction environment key for study modal trigger"
```

---

### Task 2: Wire Up fullScreenCover in MainTabView

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/MainTabView.swift`

- [ ] **Step 1: Add modal state and fullScreenCover**

Add two `@State` properties after the existing state declarations:

```swift
@State private var showStudy = false
@State private var studyDeckId: String?
```

Replace the current `TabView { ... }` block (the one inside the `if let cardRepository, let deckRepository, let notificationService` guard) with:

```swift
let studyViewModelFactory: () -> StudyViewModel = {
    let vm = StudyViewModel(cardRepository: cardRepository)
    vm.notificationService = notificationService
    return vm
}

TabView {
    DashboardView(
        viewModel: DashboardViewModel(apiClient: authService.apiClient, deckRepository: deckRepository)
    )
    .tabItem {
        Label("Dashboard", systemImage: "chart.bar")
    }

    DeckListView(
        viewModel: DeckListViewModel(deckRepository: deckRepository),
        deckRepository: deckRepository
    )
    .tabItem {
        Label("Decks", systemImage: "rectangle.stack")
    }

    CardListView(
        viewModel: CardListViewModel(apiClient: authService.apiClient)
    )
    .tabItem {
        Label("Cards", systemImage: "rectangle.on.rectangle")
    }

    SettingsView(
        viewModel: SettingsViewModel(apiClient: authService.apiClient),
        notificationViewModel: NotificationSettingsViewModel(apiClient: authService.apiClient),
        schedulingViewModel: SchedulingSettingsViewModel(apiClient: authService.apiClient)
    )
    .tabItem {
        Label("Settings", systemImage: "gear")
    }
}
.fullScreenCover(isPresented: $showStudy) {
    NavigationStack {
        StudyView(viewModel: studyViewModelFactory(), deckId: studyDeckId)
    }
}
.environment(\.startStudy, StartStudyAction { deckId in
    studyDeckId = deckId
    showStudy = true
})
```

Key changes from the original:
- `studyViewModelFactory` is still defined locally but no longer passed to child views
- `DashboardView` init no longer takes `studyViewModelFactory`
- `DeckListView` init no longer takes `studyViewModelFactory`
- `.fullScreenCover` and `.environment(\.startStudy)` added to `TabView`

Note: This will not compile yet — `DashboardView` and `DeckListView` still expect the factory parameter. That's fixed in Tasks 4 and 5.

- [ ] **Step 2: Commit (work-in-progress)**

```bash
git add fasolt.ios/Fasolt/Views/MainTabView.swift
git commit -m "feat(ios): add fullScreenCover and startStudy environment to MainTabView (WIP)"
```

---

### Task 3: Update StudyView Toolbar Layout

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/StudyView.swift`

- [ ] **Step 1: Swap toolbar item positions and remove navigationBarBackButtonHidden**

Replace the entire `.navigationBarBackButtonHidden(true)` and `.toolbar { ... }` block (lines 32–81) with:

```swift
.toolbar {
    ToolbarItem(placement: .topBarLeading) {
        if viewModel.state == .studying || viewModel.state == .flipped {
            HStack(spacing: 16) {
                Button {
                    let generator = UIImpactFeedbackGenerator(style: .light)
                    generator.impactOccurred()
                    Task {
                        await viewModel.suspendCard()
                        if viewModel.state == .summary {
                            let notification = UINotificationFeedbackGenerator()
                            notification.notificationOccurred(.success)
                        }
                    }
                } label: {
                    Image(systemName: "pause.circle")
                        .foregroundStyle(.secondary)
                }
                Button {
                    let generator = UIImpactFeedbackGenerator(style: .light)
                    generator.impactOccurred()
                    viewModel.skipCard()
                    if viewModel.state == .summary {
                        let notification = UINotificationFeedbackGenerator()
                        notification.notificationOccurred(.success)
                    }
                } label: {
                    Text("Skip")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
            }
        }
    }
    ToolbarItem(placement: .topBarTrailing) {
        if viewModel.state != .summary {
            Button {
                if (viewModel.cardsStudied > 0 || viewModel.skippedCount > 0) && viewModel.state != .summary {
                    viewModel.state = .summary
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
```

Changes:
- Removed `.navigationBarBackButtonHidden(true)` — no back button in a modal
- X button moved from `topBarLeading` to `topBarTrailing`
- Pause/Skip moved from `topBarTrailing` to `topBarLeading`

- [ ] **Step 2: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Study/StudyView.swift
git commit -m "feat(ios): move X button to trailing, pause/skip to leading in StudyView"
```

---

### Task 4: Update DashboardView to Use Environment Action

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift`

- [ ] **Step 1: Replace factory parameter with environment**

Remove the `studyViewModelFactory` property and add the environment variable. Replace:

```swift
@State private var viewModel: DashboardViewModel
private let studyViewModelFactory: () -> StudyViewModel

init(viewModel: DashboardViewModel, studyViewModelFactory: @escaping () -> StudyViewModel) {
    _viewModel = State(initialValue: viewModel)
    self.studyViewModelFactory = studyViewModelFactory
}
```

With:

```swift
@Environment(\.startStudy) private var startStudy
@State private var viewModel: DashboardViewModel

init(viewModel: DashboardViewModel) {
    _viewModel = State(initialValue: viewModel)
}
```

- [ ] **Step 2: Remove study navigation state and destinations**

Delete these three `@State` properties:

```swift
@State private var showStudy = false
@State private var selectedDeckId: String?
@State private var showDeckStudy = false
```

Remove the `.navigationDestination(isPresented: $showDeckStudy)` modifier from the `NavigationStack` body (the one at line 69):

```swift
.navigationDestination(isPresented: $showDeckStudy) {
    StudyView(viewModel: studyViewModelFactory(), deckId: selectedDeckId)
}
```

- [ ] **Step 3: Update hero card to use environment action**

In `heroCard`, replace the `.onTapGesture` and `.navigationDestination` block:

```swift
.onTapGesture {
    if viewModel.dueCount > 0 {
        showStudy = true
    }
}
.navigationDestination(isPresented: $showStudy) {
    StudyView(viewModel: studyViewModelFactory())
}
```

With just:

```swift
.onTapGesture {
    if viewModel.dueCount > 0 {
        startStudy()
    }
}
```

- [ ] **Step 4: Update deck row to use environment action**

In `deckSection`, replace the deck row button action:

```swift
Button {
    selectedDeckId = deck.id
    showDeckStudy = true
} label: {
```

With:

```swift
Button {
    startStudy(deckId: deck.id)
} label: {
```

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift
git commit -m "feat(ios): use startStudy environment action in DashboardView"
```

---

### Task 5: Update DeckListView — Remove Factory Parameter

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Decks/DeckListView.swift`

- [ ] **Step 1: Remove studyViewModelFactory**

Replace the property declarations and init:

```swift
@State private var viewModel: DeckListViewModel
@State private var searchText = ""
@State private var sortOrder: DeckSortOrder = .name
private let deckRepository: DeckRepository
private let studyViewModelFactory: () -> StudyViewModel

init(
    viewModel: DeckListViewModel,
    deckRepository: DeckRepository,
    studyViewModelFactory: @escaping () -> StudyViewModel
) {
    _viewModel = State(initialValue: viewModel)
    self.deckRepository = deckRepository
    self.studyViewModelFactory = studyViewModelFactory
}
```

With:

```swift
@State private var viewModel: DeckListViewModel
@State private var searchText = ""
@State private var sortOrder: DeckSortOrder = .name
private let deckRepository: DeckRepository

init(
    viewModel: DeckListViewModel,
    deckRepository: DeckRepository
) {
    _viewModel = State(initialValue: viewModel)
    self.deckRepository = deckRepository
}
```

- [ ] **Step 2: Remove factory from DeckDetailView instantiation**

In the `NavigationLink` inside the list, replace:

```swift
DeckDetailView(
    viewModel: DeckDetailViewModel(
        deckRepository: deckRepository,
        deckId: deck.id,
        deckName: deck.name
    ),
    studyViewModelFactory: studyViewModelFactory
)
```

With:

```swift
DeckDetailView(
    viewModel: DeckDetailViewModel(
        deckRepository: deckRepository,
        deckId: deck.id,
        deckName: deck.name
    )
)
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Decks/DeckListView.swift
git commit -m "feat(ios): remove studyViewModelFactory from DeckListView"
```

---

### Task 6: Update DeckDetailView — Use Environment Action

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Decks/DeckDetailView.swift`

- [ ] **Step 1: Replace factory with environment**

Replace:

```swift
struct DeckDetailView: View {
    @State private var viewModel: DeckDetailViewModel
    @State private var sortOrder: CardSortOrder = .dueDate
    private let studyViewModelFactory: () -> StudyViewModel

    init(
        viewModel: DeckDetailViewModel,
        studyViewModelFactory: @escaping () -> StudyViewModel
    ) {
        _viewModel = State(initialValue: viewModel)
        self.studyViewModelFactory = studyViewModelFactory
    }
```

With:

```swift
struct DeckDetailView: View {
    @Environment(\.startStudy) private var startStudy
    @State private var viewModel: DeckDetailViewModel
    @State private var sortOrder: CardSortOrder = .dueDate

    init(viewModel: DeckDetailViewModel) {
        _viewModel = State(initialValue: viewModel)
    }
```

- [ ] **Step 2: Replace NavigationLink with Button for study**

Replace the "Study This Deck" `NavigationLink` block:

```swift
if detail.dueCount > 0 && !detail.isSuspended {
    NavigationLink {
        StudyView(viewModel: studyViewModelFactory(), deckId: viewModel.deckId)
    } label: {
        Text("Study This Deck")
            .font(.headline)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
    }
    .buttonStyle(.borderedProminent)
    .padding()
}
```

With:

```swift
if detail.dueCount > 0 && !detail.isSuspended {
    Button {
        startStudy(deckId: viewModel.deckId)
    } label: {
        Text("Study This Deck")
            .font(.headline)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
    }
    .buttonStyle(.borderedProminent)
    .padding()
}
```

- [ ] **Step 3: Build to verify everything compiles**

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Decks/DeckDetailView.swift
git commit -m "feat(ios): use startStudy environment action in DeckDetailView"
```

---

### Task 7: Manual Testing in Simulator

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh` (or start backend + frontend manually)

- [ ] **Step 2: Build and run in simulator**

Open Xcode, run on iPhone 16 simulator (or via `xcodebuild`). Log in with the dev seed user.

- [ ] **Step 3: Test dashboard hero card entry**

1. On the Dashboard, tap the "Cards due" hero card
2. Verify: study view slides up as a full-screen modal (no tab bar visible)
3. Verify: X button is in the top-right corner
4. Verify: Pause and Skip are in the top-left corner
5. Tap X → should return to dashboard

- [ ] **Step 4: Test dashboard deck row entry**

1. On the Dashboard, tap a deck in the "Study by deck" section
2. Verify: full-screen modal opens, studying only that deck's cards
3. Complete or dismiss the session

- [ ] **Step 5: Test deck detail entry**

1. Go to Decks tab → tap a deck → tap "Study This Deck"
2. Verify: full-screen modal opens
3. Study some cards, verify summary shows, tap "Done"
4. Verify: returns to the deck detail view

- [ ] **Step 6: Verify no swipe-back gesture**

While in the study modal, try swiping from the left edge. It should NOT navigate back (modals don't support swipe-back).

- [ ] **Step 7: Commit (if any fixes were needed)**

```bash
git add -A
git commit -m "fix(ios): adjustments from manual testing of fullscreen study"
```
