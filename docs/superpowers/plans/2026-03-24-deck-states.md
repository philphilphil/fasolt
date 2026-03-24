# Deck States Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add active/inactive toggle to decks so inactive decks and their cards are excluded from study.

**Architecture:** `IsActive` boolean on Deck entity. Card activity derived at query time: active if no decks or at least one active deck. Backend filters due cards/stats, frontend dims inactive decks and hides them from study dashboard.

**Tech Stack:** .NET 10, EF Core, Vue 3 + Pinia + shadcn-vue, Swift/SwiftUI (iOS)

**Spec:** `docs/superpowers/specs/2026-03-24-deck-states-design.md`

---

## File Structure

**Backend — Modify:**
- `fasolt.Server/Domain/Entities/Deck.cs` — Add `IsActive` property
- `fasolt.Server/Application/Dtos/DeckDtos.cs` — Add `IsActive` to DTOs, add `SetDeckActiveRequest`
- `fasolt.Server/Application/Services/DeckService.cs` — Add `SetActive` method, update DTO construction
- `fasolt.Server/Application/Services/OverviewService.cs` — Filter study-inactive cards
- `fasolt.Server/Api/Endpoints/DeckEndpoints.cs` — Add `PUT /{id}/active` endpoint
- `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` — Filter study-inactive cards from due/stats
- `fasolt.Server/Api/McpTools/DeckTools.cs` — Add `SetDeckActive` tool

**Frontend — Modify:**
- `fasolt.client/src/types/index.ts` — Add `isActive` to Deck types
- `fasolt.client/src/stores/decks.ts` — Add `setActive` action
- `fasolt.client/src/views/DecksView.vue` — Dim inactive decks, sort to bottom
- `fasolt.client/src/views/DeckDetailView.vue` — Toggle button, hide study button when inactive
- `fasolt.client/src/views/StudyView.vue` — Exclude inactive decks

**iOS — Modify:**
- `fasolt.ios/Fasolt/Models/APIModels.swift` — Add `isActive` to DTOs
- `fasolt.ios/Fasolt/Models/CachedDeck.swift` — Add `isActive` property
- `fasolt.ios/Fasolt/Repositories/DeckRepository.swift` — Cache `isActive`
- `fasolt.ios/Fasolt/Views/Decks/DeckListView.swift` — Dim inactive, sort to bottom
- `fasolt.ios/Fasolt/Views/Decks/DeckDetailView.swift` — Toggle button, hide study button

**Database:**
- New EF Core migration for `IsActive` column

---

### Task 1: Add IsActive to Deck Entity and Migration

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Deck.cs`
- New migration in `fasolt.Server/Infrastructure/Data/Migrations/`

- [ ] **Step 1: Add IsActive property to Deck entity**

In `fasolt.Server/Domain/Entities/Deck.cs`, add after the `CreatedAt` property (line 11):

```csharp
public bool IsActive { get; set; } = true;
```

- [ ] **Step 2: Generate the migration**

Run: `dotnet ef migrations add AddDeckIsActive --project fasolt.Server`

- [ ] **Step 3: Apply the migration**

Run: `dotnet ef database update --project fasolt.Server`

- [ ] **Step 4: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Domain/Entities/Deck.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat(decks): add IsActive property to Deck entity with migration"
```

---

### Task 2: Update DTOs and DeckService

**Files:**
- Modify: `fasolt.Server/Application/Dtos/DeckDtos.cs`
- Modify: `fasolt.Server/Application/Services/DeckService.cs`

- [ ] **Step 1: Add IsActive to DeckDto and DeckDetailDto, add SetDeckActiveRequest**

In `fasolt.Server/Application/Dtos/DeckDtos.cs`, change:

```csharp
public record DeckDto(string Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt);
```
to:
```csharp
public record DeckDto(string Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt, bool IsActive);
```

Change:
```csharp
public record DeckDetailDto(string Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards);
```
to:
```csharp
public record DeckDetailDto(string Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards, bool IsActive);
```

Add new request DTO:
```csharp
public record SetDeckActiveRequest(bool IsActive);
```

- [ ] **Step 2: Update all DeckDto construction sites in DeckService to include IsActive**

In `DeckService.cs`, update these methods:

**CreateDeck** (line 27) — change:
```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, 0, 0, deck.CreatedAt);
```
to:
```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, 0, 0, deck.CreatedAt, deck.IsActive);
```

**ListDecks** (lines 36-43) — change the Select to include `d.IsActive`:
```csharp
.Select(d => new DeckDto(
    d.PublicId,
    d.Name,
    d.Description,
    d.Cards.Count,
    d.Cards.Count(dc => dc.Card.DueAt == null || dc.Card.DueAt <= now),
    d.CreatedAt,
    d.IsActive))
```

**GetDeck** (line 63) — change:
```csharp
return new DeckDetailDto(deck.PublicId, deck.Name, deck.Description, cards.Count, dueCount, cards);
```
to:
```csharp
return new DeckDetailDto(deck.PublicId, deck.Name, deck.Description, cards.Count, dueCount, cards, deck.IsActive);
```

**UpdateDeck** (line 82) — change:
```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt);
```
to:
```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt, deck.IsActive);
```

- [ ] **Step 2: Add SetActive method**

Add to `DeckService.cs` after the `UpdateDeck` method:

```csharp
public async Task<DeckDto?> SetActive(string userId, string publicId, bool isActive)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

    if (deck is null) return null;

    deck.IsActive = isActive;
    await db.SaveChangesAsync();

    var now = DateTimeOffset.UtcNow;
    var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
    var dueCount = await db.DeckCards.CountAsync(dc =>
        dc.DeckId == deck.Id && (dc.Card.DueAt == null || dc.Card.DueAt <= now));

    return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt, deck.IsActive);
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Dtos/DeckDtos.cs fasolt.Server/Application/Services/DeckService.cs
git commit -m "feat(decks): add IsActive to DTOs and update DeckService"
```

---

### Task 3: Update Review Endpoints (Due Cards & Stats)

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`

- [ ] **Step 1: Add study-active filter to GetDueCards**

In `ReviewEndpoints.cs`, the `GetDueCards` method (line 49-75). After the initial query (line 58-59):

```csharp
var query = db.Cards
    .Where(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));
```

Add the study-active filter immediately after:
```csharp
// Exclude cards that are only in inactive decks
query = query.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive));
```

Also, when filtering by deckId (lines 61-65), after finding the deck, check if it's active:
```csharp
if (deckId is not null)
{
    var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == user.Id);
    if (deck is null) return Results.NotFound();
    if (!deck.IsActive) return Results.Ok(Array.Empty<DueCardDto>());
    query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
}
```

- [ ] **Step 2: Add study-active filter to GetStats**

In the `GetStats` method (lines 122-136), add the study-active filter to all three counts. The filter expression is:
```csharp
c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive)
```

Change the method body to:

```csharp
var now = DateTimeOffset.UtcNow;

// Study-active filter: cards with no decks OR at least one active deck
var activeCards = db.Cards
    .Where(c => c.UserId == user.Id)
    .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive));

var dueCount = await activeCards.CountAsync(c => c.DueAt == null || c.DueAt <= now);
var totalCards = await activeCards.CountAsync();
var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
var studiedToday = await activeCards.CountAsync(c =>
    c.LastReviewedAt != null && c.LastReviewedAt >= todayStart);

return Results.Ok(new ReviewStatsDto(dueCount, totalCards, studiedToday));
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/ReviewEndpoints.cs
git commit -m "feat(decks): filter study-inactive cards from due cards and stats"
```

---

### Task 4: Update OverviewService

**Files:**
- Modify: `fasolt.Server/Application/Services/OverviewService.cs`

- [ ] **Step 1: Add study-active filter to overview counts**

In `OverviewService.cs`, update `GetOverview` to filter study-inactive cards. Replace the existing method body with:

```csharp
public async Task<OverviewDto> GetOverview(string userId)
{
    var now = DateTimeOffset.UtcNow;

    // Study-active cards: no decks OR at least one active deck
    var activeCards = db.Cards
        .Where(c => c.UserId == userId)
        .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive));

    var totalCards = await activeCards.CountAsync();

    var dueCards = await activeCards.CountAsync(c =>
        c.DueAt == null || c.DueAt <= now);

    var stateCounts = await activeCards
        .GroupBy(c => c.State)
        .Select(g => new { State = g.Key, Count = g.Count() })
        .ToListAsync();

    var cardsByState = AllStates.ToDictionary(
        s => s,
        s => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0);

    var totalDecks = await db.Decks.CountAsync(d => d.UserId == userId);

    var totalSources = await activeCards
        .Where(c => c.SourceFile != null)
        .Select(c => c.SourceFile)
        .Distinct()
        .CountAsync();

    return new OverviewDto(totalCards, dueCards, cardsByState, totalDecks, totalSources);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Application/Services/OverviewService.cs
git commit -m "feat(decks): filter study-inactive cards from overview stats"
```

---

### Task 5: Add SetActive Endpoint and MCP Tool

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`
- Modify: `fasolt.Server/Api/McpTools/DeckTools.cs`

- [ ] **Step 1: Add PUT endpoint for setting deck active state**

In `DeckEndpoints.cs`, add to the route mapping (after line 20, the RemoveCard route):

```csharp
group.MapPut("/{id}/active", SetActive);
```

Add the handler method:

```csharp
private static async Task<IResult> SetActive(
    string id,
    SetDeckActiveRequest request,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    DeckService deckService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var dto = await deckService.SetActive(user.Id, id, request.IsActive);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}
```

- [ ] **Step 2: Add SetDeckActive MCP tool**

In `DeckTools.cs`, add after the `DeleteDeck` method:

```csharp
[McpServerTool, Description("Set a deck's active state. Inactive decks and their cards are excluded from study/review. Cards in multiple decks remain active if at least one deck is active.")]
public async Task<string> SetDeckActive(
    [Description("ID of the deck")] string deckId,
    [Description("true to activate, false to deactivate")] bool isActive)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);
    var result = await deckService.SetActive(userId, deckId, isActive);
    if (result is null)
        return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);
    return JsonSerializer.Serialize(result, McpJson.Options);
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/DeckEndpoints.cs fasolt.Server/Api/McpTools/DeckTools.cs
git commit -m "feat(decks): add SetActive endpoint and MCP tool"
```

---

### Task 6: Update Frontend Types and Store

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Modify: `fasolt.client/src/stores/decks.ts`

- [ ] **Step 1: Add isActive to TypeScript types**

In `fasolt.client/src/types/index.ts`, add `isActive` to the `Deck` interface (after `createdAt`, line 32):

```typescript
isActive: boolean
```

Since `DeckDetail extends Deck`, it inherits `isActive` automatically.

- [ ] **Step 2: Add setActive action to decks store**

In `fasolt.client/src/stores/decks.ts`, add after the `removeCard` method:

```typescript
async function setActive(id: string, isActive: boolean): Promise<Deck> {
    const result = await apiFetch<Deck>(`/decks/${id}/active`, {
      method: 'PUT',
      body: JSON.stringify({ isActive }),
    })
    const idx = decks.value.findIndex(d => d.id === id)
    if (idx !== -1) decks.value[idx] = result
    return result
  }
```

Add `setActive` to the return object.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/types/index.ts fasolt.client/src/stores/decks.ts
git commit -m "feat(decks): add isActive to frontend types and store"
```

---

### Task 7: Update DecksView (Inactive Visual Treatment)

**Files:**
- Modify: `fasolt.client/src/views/DecksView.vue`

- [ ] **Step 1: Sort inactive decks to bottom and add visual treatment**

In `DecksView.vue`, the deck grid iterates `decks.decks` (line 57). Add a computed that sorts inactive to bottom:

Add `computed` to the existing `vue` import (which already imports `ref, onMounted`):

```typescript
const sortedDecks = computed(() =>
  [...decksStore.decks].sort((a, b) => {
    if (a.isActive !== b.isActive) return a.isActive ? -1 : 1
    return 0
  })
)
```

Replace `v-for="deck in decks.decks"` (line 58) with `v-for="deck in sortedDecks"`.

Add opacity and badge to the Card component. On the `<Card>` element (line 59), add a conditional class:

```vue
:class="{ 'opacity-50': !deck.isActive }"
```

Add an "Inactive" badge next to the due count. After the due count span (around line 68), add:

```vue
<Badge v-if="!deck.isActive" variant="outline" class="text-[10px] ml-2">Inactive</Badge>
```

Import `Badge` from `@/components/ui/badge`.

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/DecksView.vue
git commit -m "feat(decks): dim inactive decks and sort to bottom in deck list"
```

---

### Task 8: Update DeckDetailView (Toggle Button)

**Files:**
- Modify: `fasolt.client/src/views/DeckDetailView.vue`

- [ ] **Step 1: Add activate/deactivate toggle button**

In `DeckDetailView.vue`, import the decks store and add a toggle handler. In the `<script setup>`:

```typescript
import { useDecksStore } from '@/stores/decks'
const decksStore = useDecksStore()

async function toggleActive() {
  if (!deck.value) return
  const updated = await decksStore.setActive(deck.value.id, !deck.value.isActive)
  deck.value = { ...deck.value, isActive: updated.isActive }
}
```

Add the toggle button in the header area, near the existing edit/delete buttons (around lines 170-175). Add alongside the existing buttons:

```vue
<Button
  variant="outline"
  size="sm"
  class="text-xs"
  @click="toggleActive"
>
  {{ deck.isActive ? 'Deactivate' : 'Activate' }}
</Button>
```

- [ ] **Step 2: Hide "Study this deck" button when inactive**

The existing study button (lines 176-183) has `v-if="deck.dueCount > 0"`. Change it to:

```vue
v-if="deck.dueCount > 0 && deck.isActive"
```

- [ ] **Step 3: Add inactive banner**

Add a visual indicator when the deck is inactive. Place above the stat bar:

```vue
<div v-if="!deck.isActive" class="rounded-md border border-muted bg-muted/50 px-4 py-2 text-xs text-muted-foreground">
  This deck is inactive. Cards are excluded from study.
</div>
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/DeckDetailView.vue
git commit -m "feat(decks): add activate/deactivate toggle in deck detail"
```

---

### Task 9: Update StudyView (Exclude Inactive Decks)

**Files:**
- Modify: `fasolt.client/src/views/StudyView.vue`

- [ ] **Step 1: Filter inactive decks from "Study by deck" section**

In `StudyView.vue`, the deck list iterates `decksStore.decks` (line 59). Add a computed that filters to active only:

```typescript
const activeDecks = computed(() => decksStore.decks.filter(d => d.isActive))
```

Replace `v-for="deck in decksStore.decks"` with `v-for="deck in activeDecks"`.

Import `computed` from `vue` if not already imported.

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/StudyView.vue
git commit -m "feat(decks): exclude inactive decks from study dashboard"
```

---

### Task 10: Update iOS Models and Repository

**Files:**
- Modify: `fasolt.ios/Fasolt/Models/APIModels.swift`
- Modify: `fasolt.ios/Fasolt/Models/CachedDeck.swift`
- Modify: `fasolt.ios/Fasolt/Repositories/DeckRepository.swift`

- [ ] **Step 1: Add isActive to DeckDTO and DeckDetailDTO**

In `APIModels.swift`, add to `DeckDTO` (after `createdAt`, line 69):
```swift
let isActive: Bool
```

Add to `DeckDetailDTO` (after `dueCount`, line 77):
```swift
let isActive: Bool
```

- [ ] **Step 2: Add isActive to CachedDeck**

In `CachedDeck.swift`, add property after `createdAt` (line 11). **Critical: must have a default value for SwiftData lightweight migration:**
```swift
var isActive: Bool = true
```

Update the initializer (lines 15-19) to include `isActive`:
```swift
init(
    publicId: String, name: String, deckDescription: String? = nil,
    cardCount: Int = 0, dueCount: Int = 0, createdAt: Date = .now,
    isActive: Bool = true
) {
    self.publicId = publicId
    self.name = name
    self.deckDescription = deckDescription
    self.cardCount = cardCount
    self.dueCount = dueCount
    self.createdAt = createdAt
    self.isActive = isActive
}
```

- [ ] **Step 3: Update DeckRepository cache write methods**

In `DeckRepository.swift`, update `cacheDeckList` (around line 73) to include `isActive`:

In the existing deck update block:
```swift
if let deck = existing {
    deck.name = dto.name
    deck.deckDescription = dto.description
    deck.cardCount = dto.cardCount
    deck.dueCount = dto.dueCount
    deck.isActive = dto.isActive
}
```

In the new deck creation block:
```swift
let deck = CachedDeck(
    publicId: dto.id,
    name: dto.name,
    deckDescription: dto.description,
    cardCount: dto.cardCount,
    dueCount: dto.dueCount,
    isActive: dto.isActive
)
```

- [ ] **Step 4: Update DeckRepository cache read methods**

In `loadCachedDecks()` (line 144), update the `DeckDTO` construction to include `isActive`:
```swift
return cached.map { deck in
    DeckDTO(
        id: deck.publicId,
        name: deck.name,
        description: deck.deckDescription,
        cardCount: deck.cardCount,
        dueCount: deck.dueCount,
        createdAt: DateFormatters.iso8601.string(from: deck.createdAt),
        isActive: deck.isActive
    )
}
```

In `loadCachedDeckDetail()` (line 174), update the `DeckDetailDTO` construction:
```swift
return DeckDetailDTO(
    id: deck.publicId,
    name: deck.name,
    description: deck.deckDescription,
    cardCount: deck.cardCount,
    dueCount: deck.dueCount,
    isActive: deck.isActive,
    cards: cards
)
```

- [ ] **Step 5: Add setActive method to DeckRepository**

Add a new method to `DeckRepository`:
```swift
func setActive(id: String, isActive: Bool) async throws -> DeckDTO {
    let endpoint = Endpoint(
        path: "/api/decks/\(id)/active",
        method: .put,
        body: ["isActive": isActive]
    )
    let result: DeckDTO = try await apiClient.request(endpoint)

    // Update cache
    let predicate = #Predicate<CachedDeck> { $0.publicId == id }
    if let cached = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first {
        cached.isActive = result.isActive
        try? modelContext.save()
    }

    return result
}
```

- [ ] **Step 6: Commit**

```bash
git add fasolt.ios/
git commit -m "feat(decks): add isActive to iOS models and repository"
```

---

### Task 11: Update iOS Deck Views

**Files:**
- Modify: `fasolt.ios/Fasolt/ViewModels/DeckDetailViewModel.swift`
- Modify: `fasolt.ios/Fasolt/Views/Decks/DeckListView.swift`
- Modify: `fasolt.ios/Fasolt/Views/Decks/DeckDetailView.swift`

- [ ] **Step 1: Add toggleActive to DeckDetailViewModel**

In `fasolt.ios/Fasolt/ViewModels/DeckDetailViewModel.swift`, add a `toggleActive()` method after `loadDetail()`:

```swift
func toggleActive() async {
    guard let current = detail else { return }
    let newState = !current.isActive

    do {
        _ = try await deckRepository.setActive(id: deckId, isActive: newState)
        await loadDetail() // Reload to get fresh data
    } catch {
        logger.error("Failed to toggle deck active state: \(error)")
        errorMessage = "Could not update deck status."
    }
}
```

- [ ] **Step 2: Update DeckListView — dim inactive decks, sort to bottom**

In `DeckListView.swift`, update the `sortedDecks` function to sort inactive decks to the bottom. The existing function (find it in the file) sorts by the selected `sortOrder`. Add inactive-last sorting by wrapping: sort by `isActive` first (active before inactive), then by the existing sort order.

In the `deckRow` function (line 121), wrap the deck name in an HStack to add an "Inactive" badge, and add `.opacity` to the whole row:

After `Text(deck.name).font(.body.weight(.medium))` (line 123), add:
```swift
if !deck.isActive {
    Text("Inactive")
        .font(.caption2.weight(.medium))
        .padding(.horizontal, 6)
        .padding(.vertical, 2)
        .background(.secondary.opacity(0.2), in: Capsule())
        .foregroundStyle(.secondary)
}
```

At the end of the `deckRow` function, before the final closing brace, add:
```swift
.opacity(deck.isActive ? 1 : 0.5)
```

- [ ] **Step 3: Update DeckDetailView — toggle button and hide study**

In `DeckDetailView.swift`, for the "Study This Deck" button (around line 80), add `isActive` check:
```swift
if detail.dueCount > 0 && detail.isActive {
```

Add a toggle button in the existing toolbar (there's already a `ToolbarItem` for sort). Add a second `ToolbarItem`:
```swift
ToolbarItem(placement: .topBarTrailing) {
    Button {
        Task { await viewModel.toggleActive() }
    } label: {
        Label(
            viewModel.detail?.isActive == true ? "Deactivate" : "Activate",
            systemImage: viewModel.detail?.isActive == true ? "pause.circle" : "play.circle"
        )
    }
}
```

- [ ] **Step 4: No iOS dashboard changes needed**

The iOS `DashboardView` only shows aggregate stats from the backend's overview/stats endpoints, which are already filtered in Tasks 3-4. There is no deck list in the iOS dashboard. No changes needed.

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/
git commit -m "feat(decks): update iOS views with inactive deck support"
```

---

### Task 12: End-to-End Testing with Playwright

**Files:** None (browser testing)

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh`

- [ ] **Step 2: Test deactivating a deck**

Using Playwright MCP:
1. Login as `dev@fasolt.local` / `Dev1234!`
2. Navigate to Decks, click a deck
3. Click "Deactivate" button
4. Verify the inactive banner appears
5. Verify "Study this deck" button is hidden
6. Go back to Decks list — verify deck is dimmed with "Inactive" badge

- [ ] **Step 3: Test study dashboard excludes inactive deck**

1. Navigate to Study dashboard
2. Verify the deactivated deck is NOT in the "Study by deck" list
3. Verify due count is updated (excludes inactive deck's cards)

- [ ] **Step 4: Test reactivating a deck**

1. Navigate to the inactive deck's detail page
2. Click "Activate"
3. Verify banner disappears, study button returns
4. Check study dashboard includes the deck again

- [ ] **Step 5: Test MCP endpoint**

```bash
curl -s http://localhost:8080/api/admin/users  # verify backend is up
```

- [ ] **Step 6: Commit any fixes**

---

### Task 13: Move Requirement to Done

**Files:**
- Move: `docs/requirements/16_deck_states.md` → `docs/requirements/done/16_deck_states.md`

- [ ] **Step 1: Move the requirement file**

```bash
mv docs/requirements/16_deck_states.md docs/requirements/done/16_deck_states.md
```

- [ ] **Step 2: Commit**

```bash
git add docs/requirements/16_deck_states.md docs/requirements/done/16_deck_states.md
git commit -m "docs: move deck states requirement to done"
```
