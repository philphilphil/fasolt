# iOS: Full-Screen Study View Without Tab Bar

**Issue:** #29
**Date:** 2026-03-28

## Problem

The study screen shows the bottom tab bar, which is distracting during review, wastes vertical space, and misleadingly highlights "Dashboard" while studying.

## Solution

Present the study view as a full-screen modal (`.fullScreenCover`) triggered from a centralized state in `MainTabView`. This makes study feel like a distinct immersive mode the user enters and exits.

## Design

### Presentation

- Use `.fullScreenCover(isPresented:)` on the `TabView` in `MainTabView`
- The modal slides up and completely covers the tab bar
- Dismissed via the existing X button (top-right)

### Centralized Trigger via Environment

A custom `EnvironmentKey` provides a closure that any child view can call to start a study session:

```swift
// StartStudyAction — callable with optional deckId
struct StartStudyAction {
    let action: (String?) -> Void
    func callAsFunction(deckId: String? = nil) { action(deckId) }
}

struct StartStudyKey: EnvironmentKey {
    static let defaultValue = StartStudyAction { _ in }
}

extension EnvironmentValues {
    var startStudy: StartStudyAction { ... }
}
```

`MainTabView` holds the state and provides the action:

```swift
@State private var showStudy = false
@State private var studyDeckId: String?

TabView { ... }
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

### File Changes

**MainTabView.swift:**
- Add `showStudy: Bool` and `studyDeckId: String?` state
- Add `.fullScreenCover` modifier on the `TabView`
- Add `.environment(\.startStudy, ...)` to provide the trigger action
- Stop passing `studyViewModelFactory` to child views (they no longer need it)

**DashboardView.swift:**
- Remove `studyViewModelFactory` parameter
- Remove `showStudy`, `showDeckStudy`, `selectedDeckId` state
- Remove both `.navigationDestination` modifiers
- Hero card tap and deck row tap call `startStudy()` / `startStudy(deckId:)` from environment

**DeckDetailView.swift:**
- Remove `studyViewModelFactory` parameter
- Replace `NavigationLink` for "Study This Deck" with a `Button` that calls `startStudy(deckId:)`

**DeckListView.swift:**
- Remove `studyViewModelFactory` parameter (it only passes it through to `DeckDetailView`)

**StudyView.swift:**
- Wrap body in `NavigationStack` (needed for toolbar in modal context) — or the caller wraps it (MainTabView)
- Move X button from `topBarLeading` to `topBarTrailing`
- Move pause/skip from `topBarTrailing` to `topBarLeading`
- Remove `.navigationBarBackButtonHidden(true)` (no back button in a modal)

**New file — `StartStudyAction.swift` (in a shared location, e.g. `Views/Study/`):**
- `StartStudyAction` struct, `StartStudyKey`, and `EnvironmentValues` extension

### What Doesn't Change

- `StudyViewModel` — untouched
- `StudySummaryView` — untouched (its `onDone` closure calls `dismiss()`, which dismisses the modal)
- `CardView` — untouched
- FSRS logic, API calls, offline handling — all untouched

### Dismiss Behavior

- X button (top-right): if cards were studied, shows summary first; otherwise dismisses the modal directly. This is the existing behavior, unchanged.
- Summary screen's "Done" button: calls `dismiss()`, which dismisses the `.fullScreenCover`.

## Testing

- Start the full stack, open the iOS simulator
- Tap the dashboard hero card → study view should appear as full-screen modal, no tab bar
- Tap a deck row on the dashboard → same, filtered to that deck
- Navigate to a deck detail → tap "Study This Deck" → same modal
- Dismiss via X → should return to where the user was
- Complete a study session → summary should show, "Done" should dismiss
- Verify no swipe-back gesture (modal, not push)
