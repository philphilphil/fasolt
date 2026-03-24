# iOS Parts 3+4: Decks, Settings, Sync & Polish — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement deck browser, settings screen, offline sync, connectivity indicator, error states, and minimal haptic polish for the Fasolt iOS app.

**Architecture:** Repository pattern with `DeckRepository` (network-first, SwiftData cache fallback) paralleling existing `CardRepository`. Standalone `SyncService` flushes offline review queue. `OfflineBanner` ViewModifier for connectivity state across all screens.

**Tech Stack:** Swift 6, SwiftUI, SwiftData, iOS 17+, URLSession async/await, NWPathMonitor

**Spec:** `docs/superpowers/specs/2026-03-24-ios-parts3-4-decks-settings-sync-design.md`

---

## File Map

### New Files
| File | Responsibility |
|---|---|
| `Fasolt/Repositories/DeckRepository.swift` | Network-first deck fetching + SwiftData cache |
| `Fasolt/Services/SyncService.swift` | Flush PendingReview on connectivity restore |
| `Fasolt/ViewModels/DeckListViewModel.swift` | Deck list loading/refresh state |
| `Fasolt/ViewModels/DeckDetailViewModel.swift` | Deck detail + cards loading state |
| `Fasolt/ViewModels/SettingsViewModel.swift` | User info fetching for settings |
| `Fasolt/Views/Decks/DeckListView.swift` | Replace stub — full deck list UI |
| `Fasolt/Views/Decks/DeckDetailView.swift` | Cards in deck + "Study This Deck" |
| `Fasolt/Views/Decks/DeckCardRow.swift` | Reusable card row component |
| `Fasolt/Views/Settings/SettingsView.swift` | Replace stub — full settings UI |
| `Fasolt/Views/Shared/OfflineBanner.swift` | Reusable offline indicator ViewModifier |

### Modified Files
| File | Changes |
|---|---|
| `Fasolt/Models/APIModels.swift` | Add `DeckDetailDTO`, `DeckCardDTO` |
| `Fasolt/Repositories/CardRepository.swift` | Remove dead `flushPendingReviews()` |
| `Fasolt/Views/MainTabView.swift` | Wire DeckRepository, SyncService, OfflineBanner, pass factories |
| `Fasolt/Views/Study/StudyView.swift` | Fix mirrored text, add rating haptics, session-complete haptic, add `deckId` param |
| `Fasolt/Views/Dashboard/DashboardView.swift` | Add `.offlineBanner()` |

All paths relative to `/Users/phil/Projects/fasolt/fasolt.ios/`.

---

### Task 1: Add API DTOs

**Files:**
- Modify: `Fasolt/Models/APIModels.swift`

- [ ] **Step 1: Add DeckDetailDTO and DeckCardDTO**

Add after the existing `DeckDTO` struct (around line 69):

```swift
struct DeckDetailDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let cards: [DeckCardDTO]
}

struct DeckCardDTO: Decodable, Sendable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
    let state: String
    let dueAt: String?
}
```

- [ ] **Step 2: Build to verify no compile errors**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Models/APIModels.swift
git commit -m "feat(ios): add DeckDetailDTO and DeckCardDTO API models"
```

---

### Task 2: DeckRepository with offline cache

**Files:**
- Create: `Fasolt/Repositories/DeckRepository.swift`
- Modify: `Fasolt/Repositories/CardRepository.swift` (remove dead code)

- [ ] **Step 1: Create DeckRepository**

```swift
import Foundation
import SwiftData
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckRepository")

@MainActor
@Observable
final class DeckRepository {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext

    init(apiClient: APIClient, networkMonitor: NetworkMonitor, modelContext: ModelContext) {
        self.apiClient = apiClient
        self.networkMonitor = networkMonitor
        self.modelContext = modelContext
    }

    func fetchDecks() async throws -> [DeckDTO] {
        do {
            let endpoint = Endpoint(path: "/api/decks", method: .get)
            let decks: [DeckDTO] = try await apiClient.request(endpoint)
            logger.info("Fetched \(decks.count) decks from API")
            cacheDeckList(decks)
            return decks
        } catch let error as APIError {
            if case .networkError = error {
                logger.info("Offline — serving decks from cache")
                return loadCachedDecks()
            }
            throw error
        }
    }

    func fetchDeckDetail(id: String) async throws -> DeckDetailDTO {
        do {
            let endpoint = Endpoint(path: "/api/decks/\(id)", method: .get)
            let detail: DeckDetailDTO = try await apiClient.request(endpoint)
            logger.info("Fetched deck detail '\(detail.name)' with \(detail.cards.count) cards")
            cacheDeckDetail(detail)
            return detail
        } catch let error as APIError {
            if case .networkError = error {
                logger.info("Offline — serving deck detail from cache")
                if let cached = loadCachedDeckDetail(id: id) {
                    return cached
                }
            }
            throw error
        }
    }

    // MARK: - Cache Write

    private func cacheDeckList(_ decks: [DeckDTO]) {
        let incomingIds = Set(decks.map(\.id))

        // Remove stale decks
        let allCached = try? modelContext.fetch(FetchDescriptor<CachedDeck>())
        for cached in allCached ?? [] {
            if !incomingIds.contains(cached.publicId) {
                modelContext.delete(cached)
            }
        }

        // Upsert
        for dto in decks {
            let predicate = #Predicate<CachedDeck> { $0.publicId == dto.id }
            let existing = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first

            if let deck = existing {
                deck.name = dto.name
                deck.deckDescription = dto.description
                deck.cardCount = dto.cardCount
                deck.dueCount = dto.dueCount
            } else {
                let deck = CachedDeck(
                    publicId: dto.id,
                    name: dto.name,
                    deckDescription: dto.description,
                    cardCount: dto.cardCount,
                    dueCount: dto.dueCount
                )
                modelContext.insert(deck)
            }
        }

        try? modelContext.save()
    }

    private func cacheDeckDetail(_ detail: DeckDetailDTO) {
        let predicate = #Predicate<CachedDeck> { $0.publicId == detail.id }
        guard let deck = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first else { return }

        for cardDto in detail.cards {
            let cardPredicate = #Predicate<Card> { $0.publicId == cardDto.id }
            let existing = try? modelContext.fetch(FetchDescriptor(predicate: cardPredicate)).first

            if let card = existing {
                card.front = cardDto.front
                card.back = cardDto.back
                card.sourceFile = cardDto.sourceFile
                card.sourceHeading = cardDto.sourceHeading
                card.state = cardDto.state
                if !card.decks.contains(where: { $0.publicId == deck.publicId }) {
                    card.decks.append(deck)
                }
            } else {
                let card = Card(
                    publicId: cardDto.id,
                    front: cardDto.front,
                    back: cardDto.back,
                    sourceFile: cardDto.sourceFile,
                    sourceHeading: cardDto.sourceHeading,
                    state: cardDto.state
                )
                card.decks.append(deck)
                modelContext.insert(card)
            }
        }

        try? modelContext.save()
    }

    // MARK: - Cache Read

    private func loadCachedDecks() -> [DeckDTO] {
        let descriptor = FetchDescriptor<CachedDeck>(sortBy: [SortDescriptor(\.name)])
        guard let cached = try? modelContext.fetch(descriptor) else { return [] }
        return cached.map { deck in
            DeckDTO(
                id: deck.publicId,
                name: deck.name,
                description: deck.deckDescription,
                cardCount: deck.cardCount,
                dueCount: deck.dueCount,
                createdAt: ISO8601DateFormatter().string(from: deck.createdAt)
            )
        }
    }

    private func loadCachedDeckDetail(id: String) -> DeckDetailDTO? {
        let predicate = #Predicate<CachedDeck> { $0.publicId == id }
        guard let deck = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first else { return nil }
        let cards = deck.cards.map { card in
            DeckCardDTO(
                id: card.publicId,
                front: card.front,
                back: card.back,
                sourceFile: card.sourceFile,
                sourceHeading: card.sourceHeading,
                state: card.state,
                dueAt: card.dueAt.map { ISO8601DateFormatter().string(from: $0) }
            )
        }
        return DeckDetailDTO(
            id: deck.publicId,
            name: deck.name,
            description: deck.deckDescription,
            cardCount: deck.cardCount,
            dueCount: deck.dueCount,
            cards: cards
        )
    }
}
```

- [ ] **Step 2: Remove dead flushPendingReviews from CardRepository**

In `Fasolt/Repositories/CardRepository.swift`, delete the `flushPendingReviews()` method (lines 49-55):

```swift
// DELETE this method entirely:
func flushPendingReviews() async throws -> Int {
    let descriptor = FetchDescriptor<PendingReview>(
        predicate: #Predicate { !$0.synced }
    )
    let pending = try modelContext.fetch(descriptor)
    return pending.count
}
```

- [ ] **Step 3: Build to verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 4: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Repositories/DeckRepository.swift fasolt.ios/Fasolt/Repositories/CardRepository.swift
git commit -m "feat(ios): add DeckRepository with network-first caching, remove dead flush code"
```

---

### Task 3: SyncService

**Files:**
- Create: `Fasolt/Services/SyncService.swift`

- [ ] **Step 1: Create SyncService**

```swift
import Foundation
import SwiftData
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "SyncService")

@MainActor
@Observable
final class SyncService {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext
    var pendingCount: Int = 0

    private var wasConnected = true

    init(apiClient: APIClient, networkMonitor: NetworkMonitor, modelContext: ModelContext) {
        self.apiClient = apiClient
        self.networkMonitor = networkMonitor
        self.modelContext = modelContext
    }

    func startMonitoring() async {
        updatePendingCount()

        // Flush immediately if we have pending items and are online
        if networkMonitor.isConnected && pendingCount > 0 {
            await flushPendingReviews()
        }

        // Watch for connectivity changes
        while !Task.isCancelled {
            let isConnected = networkMonitor.isConnected

            if isConnected && !wasConnected {
                logger.info("Connectivity restored — flushing pending reviews")
                await flushPendingReviews()
            }

            wasConnected = isConnected
            try? await Task.sleep(for: .seconds(2))
        }
    }

    func flushPendingReviews() async {
        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate<PendingReview> { !$0.synced }
        )
        guard let pending = try? modelContext.fetch(descriptor), !pending.isEmpty else {
            pendingCount = 0
            return
        }

        logger.info("Flushing \(pending.count) pending reviews")

        for review in pending {
            do {
                let body = RateCardRequest(cardId: review.cardPublicId, rating: review.rating)
                let endpoint = Endpoint(path: "/api/review/rate", method: .post, body: body)
                let _: RateCardResponse = try await apiClient.request(endpoint)
                modelContext.delete(review)
                logger.info("Synced review for card \(review.cardPublicId)")
            } catch let error as APIError {
                switch error {
                case .notFound:
                    // Card deleted on server — discard silently
                    modelContext.delete(review)
                    logger.info("Card \(review.cardPublicId) not found on server — discarded")
                case .networkError:
                    // Lost connectivity mid-flush — stop, retry later
                    logger.info("Lost connectivity during flush — will retry")
                    break
                default:
                    logger.error("Failed to sync review for \(review.cardPublicId): \(error)")
                }
            } catch {
                logger.error("Unexpected error syncing review: \(error)")
            }
        }

        try? modelContext.save()
        updatePendingCount()
    }

    private func updatePendingCount() {
        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate<PendingReview> { !$0.synced }
        )
        pendingCount = (try? modelContext.fetchCount(descriptor)) ?? 0
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Services/SyncService.swift
git commit -m "feat(ios): add SyncService for offline review queue flush"
```

---

### Task 4: DeckListViewModel + DeckDetailViewModel + SettingsViewModel

**Files:**
- Create: `Fasolt/ViewModels/DeckListViewModel.swift`
- Create: `Fasolt/ViewModels/DeckDetailViewModel.swift`
- Create: `Fasolt/ViewModels/SettingsViewModel.swift`

- [ ] **Step 1: Create DeckListViewModel**

```swift
import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckList")

@MainActor
@Observable
final class DeckListViewModel {
    var decks: [DeckDTO] = []
    var isLoading = false
    var errorMessage: String?

    private let deckRepository: DeckRepository

    init(deckRepository: DeckRepository) {
        self.deckRepository = deckRepository
    }

    func loadDecks() async {
        isLoading = true
        errorMessage = nil

        do {
            decks = try await deckRepository.fetchDecks()
            logger.info("Loaded \(self.decks.count) decks")
        } catch {
            logger.error("Failed to load decks: \(error)")
            errorMessage = "Could not load decks. Pull to refresh."
        }

        isLoading = false
    }
}
```

- [ ] **Step 2: Create DeckDetailViewModel**

```swift
import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckDetail")

@MainActor
@Observable
final class DeckDetailViewModel {
    var detail: DeckDetailDTO?
    var isLoading = false
    var errorMessage: String?

    private let deckRepository: DeckRepository
    let deckId: String
    let deckName: String

    init(deckRepository: DeckRepository, deckId: String, deckName: String) {
        self.deckRepository = deckRepository
        self.deckId = deckId
        self.deckName = deckName
    }

    func loadDetail() async {
        isLoading = true
        errorMessage = nil

        do {
            detail = try await deckRepository.fetchDeckDetail(id: deckId)
            logger.info("Loaded deck detail: \(self.detail?.cards.count ?? 0) cards")
        } catch {
            logger.error("Failed to load deck detail: \(error)")
            errorMessage = "Could not load deck. Pull to refresh."
        }

        isLoading = false
    }
}
```

- [ ] **Step 3: Create SettingsViewModel**

```swift
import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Settings")

@MainActor
@Observable
final class SettingsViewModel {
    var email: String?
    var displayName: String?
    var serverURL: String?
    var isLoading = false

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadUserInfo() async {
        isLoading = true

        serverURL = apiClient.baseURL

        do {
            let endpoint = Endpoint(path: "/api/account/me", method: .get)
            let userInfo: UserInfoResponse = try await apiClient.request(endpoint)
            email = userInfo.email
            displayName = userInfo.displayName
            logger.info("Loaded user info: \(userInfo.email)")
        } catch {
            logger.error("Failed to load user info: \(error)")
            email = nil
        }

        isLoading = false
    }

    var appVersion: String {
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "?"
        let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "?"
        return "\(version) (\(build))"
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 5: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/ViewModels/DeckListViewModel.swift fasolt.ios/Fasolt/ViewModels/DeckDetailViewModel.swift fasolt.ios/Fasolt/ViewModels/SettingsViewModel.swift
git commit -m "feat(ios): add DeckList, DeckDetail, and Settings view models"
```

---

### Task 5: OfflineBanner ViewModifier

**Files:**
- Create: `Fasolt/Views/Shared/OfflineBanner.swift`

- [ ] **Step 1: Create OfflineBanner**

```swift
import SwiftUI

struct OfflineBanner: ViewModifier {
    @Environment(NetworkMonitor.self) private var networkMonitor

    func body(content: Content) -> some View {
        VStack(spacing: 0) {
            if !networkMonitor.isConnected {
                HStack(spacing: 6) {
                    Image(systemName: "wifi.slash")
                        .font(.caption2)
                    Text("Offline")
                        .font(.caption2.weight(.medium))
                }
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 6)
                .background(.ultraThinMaterial)
                .transition(.move(edge: .top).combined(with: .opacity))
            }

            content
        }
        .animation(.easeInOut(duration: 0.3), value: networkMonitor.isConnected)
    }
}

extension View {
    func offlineBanner() -> some View {
        modifier(OfflineBanner())
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -5`
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/Shared/OfflineBanner.swift
git commit -m "feat(ios): add OfflineBanner view modifier for connectivity indicator"
```

---

### Task 6: DeckListView + DeckCardRow + DeckDetailView

**Files:**
- Replace: `Fasolt/Views/Decks/DeckListView.swift`
- Create: `Fasolt/Views/Decks/DeckCardRow.swift`
- Create: `Fasolt/Views/Decks/DeckDetailView.swift`

- [ ] **Step 1: Replace DeckListView stub**

```swift
import SwiftUI

struct DeckListView: View {
    @State private var viewModel: DeckListViewModel
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

    var body: some View {
        NavigationStack {
            Group {
                if viewModel.decks.isEmpty && !viewModel.isLoading && viewModel.errorMessage == nil {
                    ContentUnavailableView(
                        "No decks yet",
                        systemImage: "rectangle.stack",
                        description: Text("Create decks via the API or MCP tools")
                    )
                } else if let error = viewModel.errorMessage, viewModel.decks.isEmpty {
                    ContentUnavailableView {
                        Label("Could not load", systemImage: "wifi.slash")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Retry") {
                            Task { await viewModel.loadDecks() }
                        }
                    }
                } else {
                    List(viewModel.decks, id: \.id) { deck in
                        NavigationLink {
                            DeckDetailView(
                                viewModel: DeckDetailViewModel(
                                    deckRepository: deckRepository,
                                    deckId: deck.id,
                                    deckName: deck.name
                                ),
                                studyViewModelFactory: studyViewModelFactory
                            )
                        } label: {
                            deckRow(deck)
                        }
                    }
                }
            }
            .refreshable {
                await viewModel.loadDecks()
            }
            .navigationTitle("Decks")
            .overlay {
                if viewModel.isLoading && viewModel.decks.isEmpty {
                    ProgressView()
                }
            }
            .offlineBanner()
            .task {
                if viewModel.decks.isEmpty {
                    await viewModel.loadDecks()
                }
            }
        }
    }

    private func deckRow(_ deck: DeckDTO) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(deck.name)
                    .font(.body.weight(.medium))
                if let description = deck.description {
                    Text(description)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            }

            Spacer()

            HStack(spacing: 12) {
                VStack(spacing: 2) {
                    Text("\(deck.cardCount)")
                        .font(.subheadline.weight(.semibold))
                    Text("cards")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }

                if deck.dueCount > 0 {
                    VStack(spacing: 2) {
                        Text("\(deck.dueCount)")
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(.orange)
                        Text("due")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .padding(.vertical, 4)
    }
}
```

- [ ] **Step 2: Create DeckCardRow**

```swift
import SwiftUI

struct DeckCardRow: View {
    let card: DeckCardDTO

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(card.front)
                .font(.body)
                .lineLimit(2)

            HStack(spacing: 8) {
                if let sourceFile = card.sourceFile {
                    Label(sourceFile, systemImage: "doc.text")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer()

                Text(card.state)
                    .font(.caption2.weight(.medium))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(stateColor.opacity(0.15), in: Capsule())
                    .foregroundStyle(stateColor)
            }
        }
        .padding(.vertical, 4)
    }

    private var stateColor: Color {
        switch card.state {
        case "new": return .green
        case "review": return .blue
        case "learning": return .orange
        case "relearning": return .red
        default: return .secondary
        }
    }
}
```

- [ ] **Step 3: Create DeckDetailView**

```swift
import SwiftUI
import UIKit

struct DeckDetailView: View {
    @State private var viewModel: DeckDetailViewModel
    @State private var selectedCard: DeckCardDTO?
    private let studyViewModelFactory: () -> StudyViewModel

    init(
        viewModel: DeckDetailViewModel,
        studyViewModelFactory: @escaping () -> StudyViewModel
    ) {
        _viewModel = State(initialValue: viewModel)
        self.studyViewModelFactory = studyViewModelFactory
    }

    var body: some View {
        Group {
            if let detail = viewModel.detail {
                VStack(spacing: 0) {
                    List {
                        Section {
                            HStack(spacing: 16) {
                                VStack(spacing: 2) {
                                    Text("\(detail.cardCount)")
                                        .font(.title3.weight(.semibold))
                                    Text("cards")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                                VStack(spacing: 2) {
                                    Text("\(detail.dueCount)")
                                        .font(.title3.weight(.semibold))
                                        .foregroundStyle(detail.dueCount > 0 ? .orange : .primary)
                                    Text("due")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 4)
                        }

                        if detail.cards.isEmpty {
                            Section {
                                ContentUnavailableView(
                                    "No cards in this deck",
                                    systemImage: "rectangle.on.rectangle.slash",
                                    description: Text("Add cards via the API or MCP tools")
                                )
                            }
                        } else {
                            Section("Cards") {
                                ForEach(detail.cards, id: \.id) { card in
                                    Button {
                                        selectedCard = card
                                    } label: {
                                        DeckCardRow(card: card)
                                    }
                                    .tint(.primary)
                                }
                            }
                        }
                    }

                    if detail.dueCount > 0 {
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
                }
            } else if let error = viewModel.errorMessage {
                ContentUnavailableView {
                    Label("Could not load", systemImage: "wifi.slash")
                } description: {
                    Text(error)
                } actions: {
                    Button("Retry") {
                        Task { await viewModel.loadDetail() }
                    }
                }
            } else {
                ProgressView()
            }
        }
        .navigationTitle(viewModel.deckName)
        .refreshable {
            await viewModel.loadDetail()
        }
        .sheet(item: $selectedCard) { card in
            cardDetailSheet(card)
        }
        .task {
            if viewModel.detail == nil {
                await viewModel.loadDetail()
            }
        }
    }

    private func cardDetailSheet(_ card: DeckCardDTO) -> some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 24) {
                    VStack(spacing: 8) {
                        Text("Front")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)
                        Text(card.front)
                            .font(.title3)
                            .multilineTextAlignment(.center)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        Text("Back")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)
                        Text(card.back)
                            .font(.title3)
                            .multilineTextAlignment(.center)
                    }

                    if let sourceFile = card.sourceFile {
                        Divider()
                        HStack(spacing: 4) {
                            Image(systemName: "doc.text")
                            Text(sourceFile)
                            if let heading = card.sourceHeading {
                                Text("·")
                                Text(heading)
                            }
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    }
                }
                .padding(24)
            }
            .navigationTitle("Card Detail")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { selectedCard = nil }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}

// Make DeckCardDTO identifiable for sheet presentation
extension DeckCardDTO: @retroactive Identifiable {}
```

- [ ] **Step 4: Build to verify**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -5`

Note: This will likely fail because `StudyView` doesn't accept `deckId` in its factory yet, and `MainTabView` hasn't been updated. That's expected — we'll wire everything in Task 8. The individual files should be syntactically correct.

- [ ] **Step 5: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/Decks/
git commit -m "feat(ios): add DeckListView, DeckDetailView, and DeckCardRow"
```

---

### Task 7: SettingsView

**Files:**
- Replace: `Fasolt/Views/Settings/SettingsView.swift`

- [ ] **Step 1: Replace SettingsView stub**

```swift
import SwiftUI

struct SettingsView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel: SettingsViewModel
    @State private var showSignOutConfirmation = false

    init(viewModel: SettingsViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            List {
                Section("Account") {
                    if viewModel.isLoading {
                        HStack {
                            Text("Loading...")
                                .foregroundStyle(.secondary)
                            Spacer()
                            ProgressView()
                        }
                    } else {
                        if let email = viewModel.email {
                            LabeledContent("Email", value: email)
                        }
                        if let serverURL = viewModel.serverURL {
                            LabeledContent("Server", value: serverURL)
                                .lineLimit(1)
                        }
                    }
                }

                Section {
                    Button("Sign Out", role: .destructive) {
                        showSignOutConfirmation = true
                    }
                }

                Section("About") {
                    LabeledContent("Version", value: viewModel.appVersion)
                }
            }
            .navigationTitle("Settings")
            .offlineBanner()
            .alert("Sign Out?", isPresented: $showSignOutConfirmation) {
                Button("Cancel", role: .cancel) {}
                Button("Sign Out", role: .destructive) {
                    authService.signOut()
                }
            } message: {
                Text("You'll need to sign in again to use Fasolt.")
            }
            .task {
                await viewModel.loadUserInfo()
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/Settings/SettingsView.swift
git commit -m "feat(ios): replace SettingsView stub with full implementation"
```

---

### Task 8: Wire everything in MainTabView + fix StudyView

**Files:**
- Modify: `Fasolt/Views/MainTabView.swift`
- Modify: `Fasolt/Views/Study/StudyView.swift`

- [ ] **Step 1: Update MainTabView to wire all dependencies**

Replace the entire content of `Fasolt/Views/MainTabView.swift`:

```swift
import SwiftUI
import SwiftData

struct MainTabView: View {
    @Environment(AuthService.self) private var authService
    @Environment(NetworkMonitor.self) private var networkMonitor
    @Environment(\.modelContext) private var modelContext

    @State private var syncService: SyncService?

    var body: some View {
        let apiClient = authService.apiClient

        let cardRepository = CardRepository(
            apiClient: apiClient,
            networkMonitor: networkMonitor,
            modelContext: modelContext
        )

        let deckRepository = DeckRepository(
            apiClient: apiClient,
            networkMonitor: networkMonitor,
            modelContext: modelContext
        )

        let studyViewModelFactory: () -> StudyViewModel = {
            StudyViewModel(cardRepository: cardRepository)
        }

        TabView {
            DashboardView(
                viewModel: DashboardViewModel(apiClient: apiClient),
                studyViewModelFactory: studyViewModelFactory
            )
            .tabItem {
                Label("Dashboard", systemImage: "chart.bar")
            }

            DeckListView(
                viewModel: DeckListViewModel(deckRepository: deckRepository),
                deckRepository: deckRepository,
                studyViewModelFactory: studyViewModelFactory
            )
            .tabItem {
                Label("Decks", systemImage: "rectangle.stack")
            }

            SettingsView(
                viewModel: SettingsViewModel(apiClient: apiClient)
            )
            .tabItem {
                Label("Settings", systemImage: "gear")
            }
        }
        .task {
            let service = SyncService(
                apiClient: apiClient,
                networkMonitor: networkMonitor,
                modelContext: modelContext
            )
            syncService = service
            await service.startMonitoring()
        }
    }
}
```

- [ ] **Step 2: Fix mirrored back text in StudyView**

In `Fasolt/Views/Study/StudyView.swift`, find the `rotation3DEffect` on the `CardView` (around line 95-99) and add a `scaleEffect` immediately after it:

```swift
// Change this section (lines ~88-104):
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
    .scaleEffect(x: viewModel.isFlipped ? -1 : 1)
    .onTapGesture {
        if !viewModel.isFlipped {
            flipWithHaptic()
        }
    }
}
```

- [ ] **Step 3: Add rating haptics and session-complete haptic in StudyView**

In `Fasolt/Views/Study/StudyView.swift`, update the `ratingButton` function to add haptic feedback:

```swift
private func ratingButton(_ label: String, color: Color, rating: String) -> some View {
    Button {
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.impactOccurred()
        Task {
            await viewModel.rateCard(rating)
            if viewModel.state == .summary {
                let notification = UINotificationFeedbackGenerator()
                notification.notificationOccurred(.success)
            }
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
```

- [ ] **Step 4: Update StudyView to accept deckId for "Study This Deck" flow**

`DeckDetailView` passes `deckId` to `StudyView`, which passes it to `startSession(deckId:)`. The existing `StudyView` doesn't accept a `deckId` param yet.

In `Fasolt/Views/Study/StudyView.swift`, update the struct to accept and pass `deckId`:

```swift
struct StudyView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: StudyViewModel
    @State private var showExitConfirmation = false
    private let deckId: String?

    init(viewModel: StudyViewModel, deckId: String? = nil) {
        _viewModel = State(initialValue: viewModel)
        self.deckId = deckId
    }
```

And update the `.task` modifier (around line 52):

```swift
.task {
    if viewModel.state == .idle {
        await viewModel.startSession(deckId: deckId)
    }
}
```

- [ ] **Step 5: Add OfflineBanner to DashboardView and StudyView**

In `Fasolt/Views/Dashboard/DashboardView.swift`, add `.offlineBanner()` after all existing modifiers on the `NavigationStack`'s content (after the `.task {}` block, around line 48). This ensures it wraps the content but doesn't interfere with the overlay modifiers:

```swift
            .task {
                await viewModel.loadStats()
            }
            .offlineBanner()
```

In `Fasolt/Views/Study/StudyView.swift`, add `.offlineBanner()` to the `Group` content so the banner is visible during study sessions (allowing offline rating). Add it after the `.task {}` block at the end of the body:

```swift
        .task {
            if viewModel.state == .idle {
                await viewModel.startSession(deckId: deckId)
            }
        }
        .offlineBanner()
```

- [ ] **Step 6: Build the full project**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt build 2>&1 | tail -20`
Expected: `** BUILD SUCCEEDED **`

Fix any compile errors that arise. Common issues:
- `DashboardView.studyViewModelFactory` signature change: The existing `DashboardView` takes `() -> StudyViewModel` but `MainTabView` now creates `(String?) -> StudyViewModel`. The dashboard factory wraps it: `{ studyViewModelFactory(nil) }`. This should work since `DashboardView.studyViewModelFactory` is `() -> StudyViewModel`.

- [ ] **Step 7: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add fasolt.ios/Fasolt/Views/MainTabView.swift fasolt.ios/Fasolt/Views/Study/StudyView.swift fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift
git commit -m "feat(ios): wire MainTabView with DeckRepository, SyncService, OfflineBanner, fix card flip"
```

---

### Task 9: End-to-end testing

**Prerequisites:** Full stack running (`./dev.sh`) and the iOS app built.

- [ ] **Step 1: Build and launch in simulator**

Run: `cd /Users/phil/Projects/fasolt/fasolt.ios && xcodegen generate && xcodebuild -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build`

Launch in simulator and sign in with `dev@fasolt.local` / `Dev1234!`.

- [ ] **Step 2: Test Deck List (IOS-3.1)**

- Navigate to "Decks" tab
- Verify deck list loads with names, card counts, due counts
- Pull to refresh — verify list updates
- If no decks exist, verify "No decks yet" empty state appears

- [ ] **Step 3: Test Deck Detail (IOS-3.2)**

- Tap a deck to open detail view
- Verify cards are listed with front text, source file, state badge
- Tap a card to open detail sheet — verify front/back/source shown
- If deck has due cards, verify "Study This Deck" button appears
- Tap "Study This Deck" — verify study session starts filtered to this deck

- [ ] **Step 4: Test Settings (IOS-3.3)**

- Navigate to "Settings" tab
- Verify email and server URL are shown
- Verify app version is shown
- Tap "Sign Out" — verify confirmation alert
- Confirm sign out — verify return to onboarding

- [ ] **Step 5: Test Offline Banner (IOS-4.2)**

- Toggle airplane mode in simulator
- Verify "Offline" banner appears on all tabs
- Navigate to Decks — verify cached decks are shown
- Turn off airplane mode — verify banner disappears

- [ ] **Step 6: Test Card Flip Fix**

- Start a study session
- Flip a card — verify the answer text is readable (not mirrored)

- [ ] **Step 7: Test Haptics**

- In study session, tap a rating button — verify light haptic
- Complete a session — verify success haptic on summary

- [ ] **Step 8: Commit any fixes from testing**

```bash
cd /Users/phil/Projects/fasolt
git add -A fasolt.ios/
git commit -m "fix(ios): address issues found during end-to-end testing"
```

---

### Task 10: Move completed requirements to done

- [ ] **Step 1: Move requirement files**

```bash
cd /Users/phil/Projects/fasolt
mv docs/requirements/18_ios_part3_decks_settings.md docs/requirements/done/
mv docs/requirements/19_ios_part4_sync_polish.md docs/requirements/done/
```

Note: IOS-4.5 (App Icon + Launch Screen) is deferred per the spec, but the requirement file covers all of Part 4 so it moves to done with the understanding that IOS-4.5 will be a separate task.

- [ ] **Step 2: Commit**

```bash
cd /Users/phil/Projects/fasolt
git add docs/requirements/
git commit -m "docs: move iOS Parts 3+4 requirements to done"
```
