# iOS CRUD & Tab Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full card and deck CRUD to the iOS app, then restructure tabs from 4 to 3 (Study | Library | Settings).

**Architecture:** Phase 1 adds CRUD via repository methods, view models, and sheet-based forms on existing views. Phase 2 restructures MainTabView into 3 tabs with a segmented Library view. Each task is self-contained with its own commit.

**Tech Stack:** Swift, SwiftUI, async/await, @Observable, APIClient actor

**Spec:** `docs/superpowers/specs/2026-03-28-ios-crud-and-tab-redesign.md`

---

## File Structure

### New Files
- `Fasolt/Views/Cards/CardFormSheet.swift` — shared create/edit sheet for cards
- `Fasolt/Views/Decks/DeckFormSheet.swift` — shared create/edit sheet for decks
- `Fasolt/Views/Library/LibraryView.swift` — segmented Decks/Cards view (Phase 2)

### Modified Files
- `Fasolt/Models/APIModels.swift` — add request DTOs for create/update/delete
- `Fasolt/Repositories/CardRepository.swift` — add create, update, delete, setSuspended methods
- `Fasolt/Repositories/DeckRepository.swift` — add create, update, delete methods
- `Fasolt/ViewModels/CardListViewModel.swift` — add create, delete, suspend actions
- `Fasolt/ViewModels/DeckListViewModel.swift` — add create, delete, suspend actions
- `Fasolt/ViewModels/DeckDetailViewModel.swift` — add edit, delete card/deck actions
- `Fasolt/Views/Cards/CardListView.swift` — add "+" toolbar button, swipe actions, sheet
- `Fasolt/Views/Decks/DeckListView.swift` — add "+" toolbar button, swipe actions, sheet
- `Fasolt/Views/Decks/DeckDetailView.swift` — add Edit button, card swipe actions, sheet
- `Fasolt/Views/Shared/CardDetailView.swift` — add Edit toolbar button
- `Fasolt/Views/MainTabView.swift` — restructure to 3 tabs (Phase 2)

---

## Phase 1: CRUD Features

### Task 1: Add API Request DTOs

**Files:**
- Modify: `Fasolt/Models/APIModels.swift`

- [ ] **Step 1: Add card and deck request types to APIModels.swift**

Add these types at the end of the file, before the closing:

```swift
// MARK: - Card Requests

struct CreateCardRequest: Encodable, Sendable {
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
}

struct UpdateCardRequest: Encodable, Sendable {
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
    let deckIds: [String]?
}

struct SetSuspendedRequest: Encodable, Sendable {
    let isSuspended: Bool
}

// MARK: - Deck Requests

struct CreateDeckRequest: Encodable, Sendable {
    let name: String
    let description: String?
}

struct UpdateDeckRequest: Encodable, Sendable {
    let name: String
    let description: String?
}
```

- [ ] **Step 2: Build and verify compilation**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Models/APIModels.swift
git commit -m "feat(ios): add CRUD request DTOs for cards and decks"
```

---

### Task 2: Add Card Repository CRUD Methods

**Files:**
- Modify: `Fasolt/Repositories/CardRepository.swift`

- [ ] **Step 1: Add createCard method**

Add after the existing `suspendCard` method:

```swift
func createCard(_ request: CreateCardRequest) async throws -> CardDTO {
    let endpoint = Endpoint(path: "/api/cards", method: .post, body: request)
    let card: CardDTO = try await apiClient.request(endpoint)
    logger.info("Created card \(card.id)")
    return card
}
```

- [ ] **Step 2: Add updateCard method**

```swift
func updateCard(id: String, _ request: UpdateCardRequest) async throws -> CardDTO {
    let endpoint = Endpoint(path: "/api/cards/\(id)", method: .put, body: request)
    let card: CardDTO = try await apiClient.request(endpoint)
    logger.info("Updated card \(card.id)")
    return card
}
```

- [ ] **Step 3: Add deleteCard method**

```swift
func deleteCard(id: String) async throws {
    let endpoint = Endpoint(path: "/api/cards/\(id)", method: .delete)
    let _ = try await apiClient.request(endpoint) as EmptyResponse
    logger.info("Deleted card \(id)")
}
```

- [ ] **Step 4: Refactor suspendCard to support unsuspend**

Replace the existing `suspendCard` method:

```swift
func setSuspended(cardId: String, isSuspended: Bool) async throws {
    let endpoint = Endpoint(
        path: "/api/cards/\(cardId)/suspended",
        method: .put,
        body: SetSuspendedRequest(isSuspended: isSuspended)
    )
    struct SuspendResponse: Decodable { let id: String }
    let _ = try await apiClient.request(endpoint) as SuspendResponse
    logger.info("\(isSuspended ? "Suspended" : "Unsuspended") card \(cardId)")
}
```

- [ ] **Step 5: Update any callers of the old suspendCard method**

Search for `suspendCard(cardId:` in the codebase. The only caller is in `StudyViewModel.swift`. Update the call from `suspendCard(cardId:)` to `setSuspended(cardId:cardId, isSuspended: true)`.

- [ ] **Step 6: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 7: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "feat(ios): add card CRUD and suspend/unsuspend to CardRepository"
```

---

### Task 3: Add Deck Repository CRUD Methods

**Files:**
- Modify: `Fasolt/Repositories/DeckRepository.swift`

- [ ] **Step 1: Add createDeck method**

Add after the existing `setSuspended` method:

```swift
func createDeck(_ request: CreateDeckRequest) async throws -> DeckDTO {
    let endpoint = Endpoint(path: "/api/decks", method: .post, body: request)
    let deck: DeckDTO = try await apiClient.request(endpoint)
    logger.info("Created deck \(deck.id)")
    return deck
}
```

- [ ] **Step 2: Add updateDeck method**

```swift
func updateDeck(id: String, _ request: UpdateDeckRequest) async throws -> DeckDTO {
    let endpoint = Endpoint(path: "/api/decks/\(id)", method: .put, body: request)
    let deck: DeckDTO = try await apiClient.request(endpoint)
    logger.info("Updated deck \(deck.id)")
    return deck
}
```

- [ ] **Step 3: Add deleteDeck method**

```swift
func deleteDeck(id: String, deleteCards: Bool) async throws {
    var queryItems: [URLQueryItem]? = nil
    if deleteCards {
        queryItems = [URLQueryItem(name: "deleteCards", value: "true")]
    }
    let endpoint = Endpoint(path: "/api/decks/\(id)", method: .delete, queryItems: queryItems)
    let _ = try await apiClient.request(endpoint) as EmptyResponse
    logger.info("Deleted deck \(id) (deleteCards: \(deleteCards))")
}
```

- [ ] **Step 4: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 5: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Repositories/DeckRepository.swift
git commit -m "feat(ios): add deck CRUD methods to DeckRepository"
```

---

### Task 4: Card Form Sheet (Create & Edit)

**Files:**
- Create: `Fasolt/Views/Cards/CardFormSheet.swift`

- [ ] **Step 1: Create CardFormSheet.swift**

```swift
import SwiftUI

struct CardFormSheet: View {
    @Environment(\.dismiss) private var dismiss

    let mode: Mode
    let decks: [DeckDTO]
    let onSave: (CreateCardRequest, String?) async throws -> Void

    @State private var front = ""
    @State private var back = ""
    @State private var sourceFile = ""
    @State private var sourceHeading = ""
    @State private var selectedDeckId: String?
    @State private var isSaving = false
    @State private var errorMessage: String?

    enum Mode {
        case create
        case edit(front: String, back: String, sourceFile: String?, sourceHeading: String?, deckId: String?)

        var title: String {
            switch self {
            case .create: return "New Card"
            case .edit: return "Edit Card"
            }
        }
    }

    init(mode: Mode, decks: [DeckDTO], onSave: @escaping (CreateCardRequest, String?) async throws -> Void) {
        self.mode = mode
        self.decks = decks
        self.onSave = onSave

        switch mode {
        case .create:
            break
        case .edit(let front, let back, let sourceFile, let sourceHeading, let deckId):
            _front = State(initialValue: front)
            _back = State(initialValue: back)
            _sourceFile = State(initialValue: sourceFile ?? "")
            _sourceHeading = State(initialValue: sourceHeading ?? "")
            _selectedDeckId = State(initialValue: deckId)
        }
    }

    private var canSave: Bool {
        !front.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        !back.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Content") {
                    TextField("Front", text: $front, axis: .vertical)
                        .lineLimit(3...6)
                    TextField("Back", text: $back, axis: .vertical)
                        .lineLimit(3...6)
                }

                Section("Source (Optional)") {
                    TextField("Source File", text: $sourceFile)
                    TextField("Heading", text: $sourceHeading)
                }

                Section("Deck (Optional)") {
                    Picker("Deck", selection: $selectedDeckId) {
                        Text("None").tag(String?.none)
                        ForEach(decks, id: \.id) { deck in
                            Text(deck.name).tag(Optional(deck.id))
                        }
                    }
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }
            }
            .navigationTitle(mode.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task { await save() }
                    }
                    .disabled(!canSave || isSaving)
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        errorMessage = nil

        let request = CreateCardRequest(
            front: front.trimmingCharacters(in: .whitespacesAndNewlines),
            back: back.trimmingCharacters(in: .whitespacesAndNewlines),
            sourceFile: sourceFile.isEmpty ? nil : sourceFile,
            sourceHeading: sourceHeading.isEmpty ? nil : sourceHeading
        )

        do {
            try await onSave(request, selectedDeckId)
            dismiss()
        } catch {
            errorMessage = "Failed to save card. Please try again."
        }

        isSaving = false
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/Cards/CardFormSheet.swift
git commit -m "feat(ios): add CardFormSheet for create and edit"
```

---

### Task 5: Deck Form Sheet (Create & Edit)

**Files:**
- Create: `Fasolt/Views/Decks/DeckFormSheet.swift`

- [ ] **Step 1: Create DeckFormSheet.swift**

```swift
import SwiftUI

struct DeckFormSheet: View {
    @Environment(\.dismiss) private var dismiss

    let mode: Mode
    let onSave: (CreateDeckRequest) async throws -> Void

    @State private var name = ""
    @State private var description = ""
    @State private var isSaving = false
    @State private var errorMessage: String?

    enum Mode {
        case create
        case edit(DeckDTO)

        var title: String {
            switch self {
            case .create: return "New Deck"
            case .edit: return "Edit Deck"
            }
        }
    }

    init(mode: Mode, onSave: @escaping (CreateDeckRequest) async throws -> Void) {
        self.mode = mode
        self.onSave = onSave

        switch mode {
        case .create:
            break
        case .edit(let deck):
            _name = State(initialValue: deck.name)
            _description = State(initialValue: deck.description ?? "")
        }
    }

    private var canSave: Bool {
        !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Details") {
                    TextField("Name", text: $name)
                    TextField("Description (Optional)", text: $description, axis: .vertical)
                        .lineLimit(2...4)
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }
            }
            .navigationTitle(mode.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task { await save() }
                    }
                    .disabled(!canSave || isSaving)
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        errorMessage = nil

        let request = CreateDeckRequest(
            name: name.trimmingCharacters(in: .whitespacesAndNewlines),
            description: description.isEmpty ? nil : description
        )

        do {
            try await onSave(request)
            dismiss()
        } catch {
            errorMessage = "Failed to save deck. Please try again."
        }

        isSaving = false
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/Decks/DeckFormSheet.swift
git commit -m "feat(ios): add DeckFormSheet for create and edit"
```

---

### Task 6: Deck List — Create, Swipe Delete & Suspend

**Files:**
- Modify: `Fasolt/ViewModels/DeckListViewModel.swift`
- Modify: `Fasolt/Views/Decks/DeckListView.swift`

- [ ] **Step 1: Add CRUD methods to DeckListViewModel**

Add these methods to `DeckListViewModel`:

```swift
func createDeck(_ request: CreateDeckRequest) async throws {
    _ = try await deckRepository.createDeck(request)
    await loadDecks()
}

func deleteDeck(id: String, deleteCards: Bool) async throws {
    try await deckRepository.deleteDeck(id: id, deleteCards: deleteCards)
    await loadDecks()
}

func setSuspended(id: String, isSuspended: Bool) async throws {
    _ = try await deckRepository.setSuspended(id: id, isSuspended: isSuspended)
    await loadDecks()
}
```

- [ ] **Step 2: Add create sheet state and toolbar button to DeckListView**

Add state variables at the top of `DeckListView`:

```swift
@State private var showCreateSheet = false
```

Add the "+" toolbar button alongside the existing sort menu in the `.toolbar` modifier:

```swift
ToolbarItem(placement: .topBarTrailing) {
    Button {
        showCreateSheet = true
    } label: {
        Label("New Deck", systemImage: "plus")
    }
}
```

Add the sheet modifier to the `NavigationStack`:

```swift
.sheet(isPresented: $showCreateSheet) {
    DeckFormSheet(mode: .create) { request in
        try await viewModel.createDeck(request)
    }
}
```

- [ ] **Step 3: Add swipe actions to deck rows**

Replace the plain `NavigationLink` in the List with a version that has swipe actions. Change the `List(sortedDecks(filteredDecks), id: \.id) { deck in` block to use `ForEach` inside the `List`:

```swift
List {
    ForEach(sortedDecks(filteredDecks), id: \.id) { deck in
        NavigationLink {
            DeckDetailView(
                viewModel: DeckDetailViewModel(
                    deckRepository: deckRepository,
                    deckId: deck.id,
                    deckName: deck.name
                )
            )
        } label: {
            deckRow(deck)
        }
        .swipeActions(edge: .trailing, allowsFullSwipe: false) {
            Button(role: .destructive) {
                deckToDelete = deck
                showDeleteConfirmation = true
            } label: {
                Label("Delete", systemImage: "trash")
            }

            Button {
                Task {
                    try? await viewModel.setSuspended(
                        id: deck.id,
                        isSuspended: !deck.isSuspended
                    )
                }
            } label: {
                Label(
                    deck.isSuspended ? "Unsuspend" : "Suspend",
                    systemImage: deck.isSuspended ? "play.circle" : "pause.circle"
                )
            }
            .tint(.orange)
        }
    }
}
```

- [ ] **Step 4: Add delete confirmation alert**

Add state variables:

```swift
@State private var deckToDelete: DeckDTO?
@State private var showDeleteConfirmation = false
```

Add the alert modifier to the `NavigationStack`:

```swift
.alert("Delete Deck", isPresented: $showDeleteConfirmation, presenting: deckToDelete) { deck in
    Button("Delete Deck Only", role: .destructive) {
        Task { try? await viewModel.deleteDeck(id: deck.id, deleteCards: false) }
    }
    Button("Delete Deck and Cards", role: .destructive) {
        Task { try? await viewModel.deleteDeck(id: deck.id, deleteCards: true) }
    }
    Button("Cancel", role: .cancel) {}
} message: { deck in
    Text("This deck has \(deck.cardCount) cards. What would you like to do?")
}
```

- [ ] **Step 5: Update the empty state message**

Change the `ContentUnavailableView` description from `"Create decks via the API or MCP tools"` to `"Tap + to create a deck"`.

- [ ] **Step 6: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 7: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "feat(ios): add deck create, swipe delete and suspend to DeckListView"
```

---

### Task 7: Deck Detail — Edit Button & Card Swipe Actions

**Files:**
- Modify: `Fasolt/ViewModels/DeckDetailViewModel.swift`
- Modify: `Fasolt/Views/Decks/DeckDetailView.swift`

- [ ] **Step 1: Add card CRUD methods to DeckDetailViewModel**

The DeckDetailViewModel needs access to CardRepository. Add it to the init and add methods:

```swift
private let cardRepository: CardRepository

init(deckRepository: DeckRepository, cardRepository: CardRepository, deckId: String, deckName: String) {
    self.deckRepository = deckRepository
    self.cardRepository = cardRepository
    self.deckId = deckId
    self.deckName = deckName
}
```

Add methods:

```swift
func deleteCard(id: String) async throws {
    try await cardRepository.deleteCard(id: id)
    await loadDetail()
}

func setCardSuspended(id: String, isSuspended: Bool) async throws {
    try await cardRepository.setSuspended(cardId: id, isSuspended: isSuspended)
    await loadDetail()
}

func updateDeck(_ request: UpdateDeckRequest) async throws {
    let updated = try await deckRepository.updateDeck(id: deckId, request)
    deckName = updated.name
    await loadDetail()
}
```

Note: `deckName` must change from `let` to `var` for the update to work.

- [ ] **Step 2: Update DeckDetailView to accept CardRepository**

DeckDetailView needs to pass `cardRepository` when creating `DeckDetailViewModel`. Since `DeckDetailView` is created from `DeckListView`, update `DeckListView` to pass it through. In `DeckListView`, add a `cardRepository` parameter:

Update DeckListView's init to also accept `cardRepository: CardRepository` and store it. Then pass it when creating `DeckDetailViewModel`:

```swift
DeckDetailView(
    viewModel: DeckDetailViewModel(
        deckRepository: deckRepository,
        cardRepository: cardRepository,
        deckId: deck.id,
        deckName: deck.name
    )
)
```

Also update `MainTabView` to pass `cardRepository` to `DeckListView`.

- [ ] **Step 3: Add edit sheet and card swipe actions to DeckDetailView**

Add state variables:

```swift
@State private var showEditSheet = false
@State private var cardToDelete: DeckCardDTO?
@State private var showDeleteCardAlert = false
```

Add an Edit toolbar button (alongside existing sort and suspend buttons):

```swift
ToolbarItem(placement: .topBarTrailing) {
    Button {
        showEditSheet = true
    } label: {
        Label("Edit", systemImage: "pencil")
    }
}
```

Add the edit sheet modifier. To convert `DeckDetailDTO` to `DeckDTO` for the form, create it inline:

```swift
.sheet(isPresented: $showEditSheet) {
    if let detail = viewModel.detail {
        DeckFormSheet(
            mode: .edit(DeckDTO(
                id: detail.id,
                name: detail.name,
                description: detail.description,
                cardCount: detail.cardCount,
                dueCount: detail.dueCount,
                createdAt: "",
                isSuspended: detail.isSuspended
            ))
        ) { request in
            try await viewModel.updateDeck(UpdateDeckRequest(
                name: request.name,
                description: request.description
            ))
        }
    }
}
```

- [ ] **Step 4: Add swipe actions to card rows in DeckDetailView**

In the `Section("Cards")` ForEach, wrap the card NavigationLink with swipe actions:

```swift
ForEach(sortedCards(detail.cards), id: \.id) { card in
    NavigationLink {
        CardDetailView(card: card)
    } label: {
        DeckCardRow(card: card, showSourceFile: true)
    }
    .swipeActions(edge: .trailing, allowsFullSwipe: false) {
        Button(role: .destructive) {
            cardToDelete = card
            showDeleteCardAlert = true
        } label: {
            Label("Delete", systemImage: "trash")
        }

        Button {
            Task {
                try? await viewModel.setCardSuspended(
                    id: card.id,
                    isSuspended: !card.isSuspended
                )
            }
        } label: {
            Label(
                card.isSuspended ? "Unsuspend" : "Suspend",
                systemImage: card.isSuspended ? "play.circle" : "pause.circle"
            )
        }
        .tint(.orange)
    }
}
```

Add the delete confirmation alert:

```swift
.alert("Delete Card", isPresented: $showDeleteCardAlert, presenting: cardToDelete) { card in
    Button("Delete", role: .destructive) {
        Task { try? await viewModel.deleteCard(id: card.id) }
    }
    Button("Cancel", role: .cancel) {}
} message: { _ in
    Text("This cannot be undone.")
}
```

- [ ] **Step 5: Update empty state message**

Change `"Add cards via the API or MCP tools"` to `"Add cards via the API, MCP tools, or the Cards tab"`.

- [ ] **Step 6: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 7: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "feat(ios): add deck edit and card swipe actions to DeckDetailView"
```

---

### Task 8: Card List — Create, Swipe Delete & Suspend

**Files:**
- Modify: `Fasolt/ViewModels/CardListViewModel.swift`
- Modify: `Fasolt/Views/Cards/CardListView.swift`

- [ ] **Step 1: Add CRUD methods and deck list to CardListViewModel**

CardListViewModel currently uses `apiClient` directly. Add repository access and CRUD methods. Add a `cardRepository` and `deckRepository` to the init:

```swift
private let cardRepository: CardRepository
private let deckRepository: DeckRepository
var availableDecks: [DeckDTO] = []

init(apiClient: APIClient, cardRepository: CardRepository, deckRepository: DeckRepository) {
    self.apiClient = apiClient
    self.cardRepository = cardRepository
    self.deckRepository = deckRepository
}
```

Add methods:

```swift
func loadDecks() async {
    do {
        availableDecks = try await deckRepository.fetchDecks()
    } catch {
        logger.error("Failed to load decks for picker: \(error)")
    }
}

func createCard(_ request: CreateCardRequest, deckId: String?) async throws {
    let card = try await cardRepository.createCard(request)
    if let deckId {
        _ = try await cardRepository.updateCard(
            id: card.id,
            UpdateCardRequest(
                front: card.front,
                back: card.back,
                sourceFile: card.sourceFile,
                sourceHeading: card.sourceHeading,
                deckIds: [deckId]
            )
        )
    }
    await loadCards()
}

func deleteCard(id: String) async throws {
    try await cardRepository.deleteCard(id: id)
    cards.removeAll { $0.id == id }
}

func setSuspended(cardId: String, isSuspended: Bool) async throws {
    try await cardRepository.setSuspended(cardId: cardId, isSuspended: isSuspended)
    await loadCards()
}
```

- [ ] **Step 2: Update MainTabView to pass repositories to CardListViewModel**

In `MainTabView.swift`, update the `CardListView` creation to pass the repositories:

```swift
CardListView(
    viewModel: CardListViewModel(
        apiClient: authService.apiClient,
        cardRepository: cardRepository,
        deckRepository: deckRepository
    )
)
```

- [ ] **Step 3: Add create sheet, swipe actions to CardListView**

Add state variables:

```swift
@State private var showCreateSheet = false
@State private var cardToDelete: CardDTO?
@State private var showDeleteAlert = false
```

Add "+" toolbar button:

```swift
ToolbarItem(placement: .topBarTrailing) {
    Button {
        showCreateSheet = true
    } label: {
        Label("New Card", systemImage: "plus")
    }
}
```

Add sheet modifier:

```swift
.sheet(isPresented: $showCreateSheet) {
    CardFormSheet(mode: .create, decks: viewModel.availableDecks) { request, deckId in
        try await viewModel.createCard(request, deckId: deckId)
    }
}
```

Load decks when the view appears (add to the `.task` modifier):

```swift
.task {
    if viewModel.cards.isEmpty {
        await viewModel.loadCards()
    }
    await viewModel.loadDecks()
}
```

- [ ] **Step 4: Add swipe actions to card rows**

Replace the `ForEach` in `cardsList` with swipe actions:

```swift
ForEach(sortedCards(viewModel.filteredCards)) { card in
    NavigationLink {
        CardDetailView(
            card: card,
            deckNames: card.decks.isEmpty ? nil : card.decks.map(\.name)
        )
    } label: {
        DeckCardRow(
            card: card,
            deckNames: card.decks.isEmpty ? nil : card.decks.map(\.name)
        )
    }
    .swipeActions(edge: .trailing, allowsFullSwipe: false) {
        Button(role: .destructive) {
            cardToDelete = card
            showDeleteAlert = true
        } label: {
            Label("Delete", systemImage: "trash")
        }

        Button {
            Task {
                try? await viewModel.setSuspended(
                    cardId: card.id,
                    isSuspended: !card.isSuspended
                )
            }
        } label: {
            Label(
                card.isSuspended ? "Unsuspend" : "Suspend",
                systemImage: card.isSuspended ? "play.circle" : "pause.circle"
            )
        }
        .tint(.orange)
    }
}
```

Add delete confirmation alert:

```swift
.alert("Delete Card", isPresented: $showDeleteAlert, presenting: cardToDelete) { card in
    Button("Delete", role: .destructive) {
        Task { try? await viewModel.deleteCard(id: card.id) }
    }
    Button("Cancel", role: .cancel) {}
} message: { _ in
    Text("This cannot be undone.")
}
```

- [ ] **Step 5: Update empty state message**

Change `"Create cards via the API or MCP tools"` to `"Tap + to create a card"`.

- [ ] **Step 6: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 7: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "feat(ios): add card create, swipe delete and suspend to CardListView"
```

---

### Task 9: Card Detail — Edit Button

**Files:**
- Modify: `Fasolt/Views/Shared/CardDetailView.swift`
- Modify: `Fasolt/ViewModels/CardListViewModel.swift`
- Modify: `Fasolt/Views/Cards/CardListView.swift`
- Modify: `Fasolt/Views/Decks/DeckDetailView.swift`

CardDetailView uses the `CardDisplayable` protocol — both `CardDTO` and `DeckCardDTO` conform to it. The edit form uses field-based `Mode.edit(front:back:sourceFile:sourceHeading:deckId:)` so it works with any card type. Both `CardDTO` and `DeckCardDTO` have `id` (from `Identifiable`).

- [ ] **Step 1: Add updateCard method to CardListViewModel**

```swift
func updateCard(id: String, _ request: UpdateCardRequest) async throws {
    _ = try await cardRepository.updateCard(id: id, request)
    await loadCards()
}
```

- [ ] **Step 2: Add edit properties to CardDetailView**

Add these properties and state to `CardDetailView`:

```swift
struct CardDetailView: View {
    let card: any CardDisplayable
    var deckNames: [String]?
    var availableDecks: [DeckDTO] = []
    var onSaveEdit: ((UpdateCardRequest) async throws -> Void)?

    @State private var showEditSheet = false
```

Add toolbar (after `.navigationBarTitleDisplayMode(.inline)`):

```swift
.toolbar {
    if onSaveEdit != nil {
        ToolbarItem(placement: .topBarTrailing) {
            Button {
                showEditSheet = true
            } label: {
                Label("Edit", systemImage: "pencil")
            }
        }
    }
}
```

Add sheet (after `.toolbar`):

```swift
.sheet(isPresented: $showEditSheet) {
    CardFormSheet(
        mode: .edit(
            front: card.front,
            back: card.back,
            sourceFile: card.sourceFile,
            sourceHeading: card.sourceHeading,
            deckId: nil
        ),
        decks: availableDecks
    ) { request, deckId in
        let updateRequest = UpdateCardRequest(
            front: request.front,
            back: request.back,
            sourceFile: request.sourceFile,
            sourceHeading: request.sourceHeading,
            deckIds: deckId.map { [$0] }
        )
        try await onSaveEdit?(updateRequest)
    }
}
```

- [ ] **Step 3: Update CardListView to pass edit closure**

In CardListView's NavigationLink:

```swift
CardDetailView(
    card: card,
    deckNames: card.decks.isEmpty ? nil : card.decks.map(\.name),
    availableDecks: viewModel.availableDecks,
    onSaveEdit: { request in
        try await viewModel.updateCard(id: card.id, request)
    }
)
```

- [ ] **Step 4: Update DeckDetailView to pass edit closure**

Add `updateCard` method to `DeckDetailViewModel`:

```swift
func updateCard(id: String, _ request: UpdateCardRequest) async throws {
    _ = try await cardRepository.updateCard(id: id, request)
    await loadDetail()
}
```

In DeckDetailView's card NavigationLink, pass the edit closure. Also add `availableDecks` state and load them:

```swift
@State private var availableDecks: [DeckDTO] = []
```

Add to the `.task` modifier:

```swift
do { availableDecks = try await deckRepository.fetchDecks() } catch {}
```

Note: DeckDetailView needs access to `deckRepository`. It already has access to the view model which has `deckRepository` — but it's private. The simplest fix: store `deckRepository` as a property on DeckDetailView (passed from DeckListView, which already has it).

Update the NavigationLink:

```swift
CardDetailView(
    card: card,
    availableDecks: availableDecks,
    onSaveEdit: { request in
        try await viewModel.updateCard(id: card.id, request)
    }
)
```

- [ ] **Step 5: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 6: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "feat(ios): add edit button to CardDetailView with form sheet"
```

---

## Phase 2: Tab Restructure

### Task 10: Create Library View with Segmented Control

**Files:**
- Create: `Fasolt/Views/Library/LibraryView.swift`

- [ ] **Step 1: Create LibraryView.swift**

```swift
import SwiftUI

enum LibrarySegment: String, CaseIterable {
    case decks = "Decks"
    case cards = "Cards"
}

struct LibraryView: View {
    @State private var selectedSegment: LibrarySegment = .decks

    let deckListViewModel: DeckListViewModel
    let cardListViewModel: CardListViewModel
    let deckRepository: DeckRepository
    let cardRepository: CardRepository

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                Picker("View", selection: $selectedSegment) {
                    ForEach(LibrarySegment.allCases, id: \.self) { segment in
                        Text(segment.rawValue).tag(segment)
                    }
                }
                .pickerStyle(.segmented)
                .padding(.horizontal)
                .padding(.vertical, 8)

                switch selectedSegment {
                case .decks:
                    DeckListContent(
                        viewModel: deckListViewModel,
                        deckRepository: deckRepository,
                        cardRepository: cardRepository
                    )
                case .cards:
                    CardListContent(
                        viewModel: cardListViewModel
                    )
                }
            }
            .navigationTitle("Library")
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`

This will fail because `DeckListContent` and `CardListContent` don't exist yet. That's expected — they'll be extracted in the next task.

- [ ] **Step 3: Commit (even if not building yet — this is a WIP file)**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/Library/LibraryView.swift
git commit -m "feat(ios): add LibraryView shell with segmented control"
```

---

### Task 11: Extract List Content from DeckListView and CardListView

**Files:**
- Modify: `Fasolt/Views/Decks/DeckListView.swift`
- Modify: `Fasolt/Views/Cards/CardListView.swift`

The current `DeckListView` and `CardListView` each wrap their content in a `NavigationStack`. For the Library view, we need the inner content without the `NavigationStack` (since `LibraryView` provides its own). Extract the inner content into separate `DeckListContent` and `CardListContent` views.

- [ ] **Step 1: Create DeckListContent in DeckListView.swift**

Add a new `DeckListContent` struct that contains the list body, toolbar items, search, refresh, sheets, and alerts — everything except the `NavigationStack` wrapper. The existing `DeckListView` becomes a thin wrapper:

```swift
struct DeckListView: View {
    @State private var viewModel: DeckListViewModel
    private let deckRepository: DeckRepository
    private let cardRepository: CardRepository

    init(viewModel: DeckListViewModel, deckRepository: DeckRepository, cardRepository: CardRepository) {
        _viewModel = State(initialValue: viewModel)
        self.deckRepository = deckRepository
        self.cardRepository = cardRepository
    }

    var body: some View {
        NavigationStack {
            DeckListContent(
                viewModel: viewModel,
                deckRepository: deckRepository,
                cardRepository: cardRepository
            )
        }
    }
}

struct DeckListContent: View {
    // Move ALL the existing DeckListView body content here
    // (Group, searchable, refreshable, navigationTitle, toolbar, overlay, offlineBanner, task, onAppear, onReceive, sheet, alert)
    // This view does NOT have its own NavigationStack
    ...
}
```

- [ ] **Step 2: Create CardListContent in CardListView.swift**

Same pattern — extract the inner content:

```swift
struct CardListView: View {
    @State private var viewModel: CardListViewModel

    init(viewModel: CardListViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            CardListContent(viewModel: viewModel)
        }
    }
}

struct CardListContent: View {
    // Move ALL the existing CardListView body content here
    // This view does NOT have its own NavigationStack
    ...
}
```

- [ ] **Step 3: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 4: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "refactor(ios): extract DeckListContent and CardListContent for Library view"
```

---

### Task 12: Restructure MainTabView to 3 Tabs

**Files:**
- Modify: `Fasolt/Views/MainTabView.swift`

- [ ] **Step 1: Replace 4-tab TabView with 3-tab structure**

Replace the TabView content in MainTabView:

```swift
TabView {
    DashboardView(
        viewModel: DashboardViewModel(apiClient: authService.apiClient, deckRepository: deckRepository)
    )
    .tabItem {
        Label("Study", systemImage: "book.fill")
    }

    LibraryView(
        deckListViewModel: DeckListViewModel(deckRepository: deckRepository),
        cardListViewModel: CardListViewModel(
            apiClient: authService.apiClient,
            cardRepository: cardRepository,
            deckRepository: deckRepository
        ),
        deckRepository: deckRepository,
        cardRepository: cardRepository
    )
    .tabItem {
        Label("Library", systemImage: "books.vertical.fill")
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
```

- [ ] **Step 2: Build and verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/MainTabView.swift
git commit -m "feat(ios): restructure to 3 tabs — Study, Library, Settings"
```

---

### Task 13: End-to-End Testing with Playwright

**Files:** None (testing only)

- [ ] **Step 1: Start the full stack**

Run: `cd /Users/phil/Projects/fasolt && ./dev.sh` (in a separate terminal)

- [ ] **Step 2: Build and run the iOS app in the simulator**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build`

Launch the app in the simulator and test:

1. **Deck CRUD:** Navigate to Library > Decks. Tap "+", create a deck with name and description, verify it appears in the list. Swipe a deck row — verify Suspend (orange) and Delete (red) buttons appear. Tap a deck, tap Edit in toolbar, change name, save — verify name updates. Swipe delete a deck — verify confirmation alert with "Delete Deck Only" / "Delete Deck and Cards" options.

2. **Card CRUD:** Switch to Library > Cards segment. Tap "+", create a card with front/back text, optionally assign a deck, save — verify it appears. Swipe a card — verify Suspend and Delete actions. Tap into card detail, tap Edit — verify form pre-populates, change text, save.

3. **Tab structure:** Verify 3 tabs: Study, Library, Settings. Verify Library has segmented control switching between Decks and Cards. Verify navigation within Library (deck detail, card detail) works correctly.

4. **Suspend toggle:** Suspend a card via swipe, verify visual change (0.5 opacity, "Suspended" badge). Swipe again — verify "Unsuspend" label, verify card returns to normal.

- [ ] **Step 3: Fix any issues found during testing**

- [ ] **Step 4: Final commit if any fixes were needed**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/
git commit -m "fix(ios): address issues found during CRUD and tab testing"
```
