# Suspend Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-card suspension (using standard SRS "suspend" terminology) and rename Deck.IsActive to Deck.IsSuspended for consistency.

**Architecture:** Add `IsSuspended` boolean to Card entity, rename Deck's `IsActive` to `IsSuspended` (inverting logic), update all services/DTOs/endpoints/MCP tools, add suspend button to web review and card table, add suspend button to iOS review.

**Tech Stack:** .NET 10, EF Core, Vue 3, TypeScript, Swift/SwiftUI

---

### Task 1: Backend — Domain Entities and Migration

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Card.cs:25` (add IsSuspended before DeckCards)
- Modify: `fasolt.Server/Domain/Entities/Deck.cs:14` (rename IsActive → IsSuspended, invert default)
- Create: new EF migration via `dotnet ef`

- [ ] **Step 1: Add IsSuspended to Card entity**

In `fasolt.Server/Domain/Entities/Card.cs`, add before the `DeckCards` line:

```csharp
public bool IsSuspended { get; set; } = false;
```

- [ ] **Step 2: Rename Deck.IsActive to Deck.IsSuspended**

In `fasolt.Server/Domain/Entities/Deck.cs`, change line 14 from:

```csharp
public bool IsActive { get; set; } = true;
```

to:

```csharp
public bool IsSuspended { get; set; } = false;
```

- [ ] **Step 3: Create EF migration**

Run:
```bash
cd fasolt.Server && dotnet ef migrations add AddCardIsSuspendedRenameDeckIsActive
```

- [ ] **Step 4: Edit the migration to handle the rename + inversion**

The auto-generated migration will drop `IsActive` and add `IsSuspended`. Edit the `Up` method to instead:

```csharp
// Rename column and invert values for Decks
migrationBuilder.RenameColumn(name: "IsActive", table: "Decks", newName: "IsSuspended");
migrationBuilder.Sql("UPDATE \"Decks\" SET \"IsSuspended\" = NOT \"IsSuspended\"");
migrationBuilder.AlterColumn<bool>(name: "IsSuspended", table: "Decks", defaultValue: false);

// Add IsSuspended to Cards
migrationBuilder.AddColumn<bool>(name: "IsSuspended", table: "Cards", nullable: false, defaultValue: false);
```

And the `Down` method to reverse:

```csharp
migrationBuilder.DropColumn(name: "IsSuspended", table: "Cards");
migrationBuilder.RenameColumn(name: "IsSuspended", table: "Decks", newName: "IsActive");
migrationBuilder.Sql("UPDATE \"Decks\" SET \"IsActive\" = NOT \"IsActive\"");
migrationBuilder.AlterColumn<bool>(name: "IsActive", table: "Decks", defaultValue: true);
```

Also update the migration's `.Designer.cs` snapshot and the `AppDbContextModelSnapshot.cs` — regenerate with `dotnet ef migrations script` or by removing and re-adding. The simplest approach: after editing the migration `Up`/`Down`, just run `dotnet build` and then `dotnet ef database update` to verify it works.

- [ ] **Step 5: Verify migration applies cleanly**

```bash
docker compose up -d
cd fasolt.Server && dotnet ef database update
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: add Card.IsSuspended, rename Deck.IsActive to IsSuspended (#45)"
```

---

### Task 2: Backend — DTOs

**Files:**
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/DeckDtos.cs`

- [ ] **Step 1: Add IsSuspended to CardDto**

In `fasolt.Server/Application/Dtos/CardDtos.cs`, change the `CardDto` record (lines 12-19) to include `IsSuspended`:

```csharp
public record CardDto(
    string Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks,
    bool IsSuspended = false,
    DateTimeOffset? DueAt = null, double? Stability = null,
    double? Difficulty = null, int? Step = null,
    DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);
```

Change `CardDeckInfoDto` (line 20) from `IsActive` to `IsSuspended`:

```csharp
public record CardDeckInfoDto(string Id, string Name, bool IsSuspended);
```

- [ ] **Step 2: Update DeckDtos**

In `fasolt.Server/Application/Dtos/DeckDtos.cs`:

Change `DeckDto` (line 7):
```csharp
public record DeckDto(string Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt, bool IsSuspended);
```

Change `DeckDetailDto` (line 9):
```csharp
public record DeckDetailDto(string Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards, bool IsSuspended);
```

Change `SetDeckActiveRequest` (line 11) to:
```csharp
public record SetDeckSuspendedRequest(bool IsSuspended);
```

Add `DeckCardDto.IsSuspended` — change the record (lines 13-19):
```csharp
public record DeckCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, DateTimeOffset? DueAt,
    bool IsSuspended = false,
    double? Stability = null, double? Difficulty = null,
    int? Step = null, DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: update DTOs for IsSuspended (#45)"
```

---

### Task 3: Backend — Services

**Files:**
- Modify: `fasolt.Server/Application/Services/ReviewService.cs`
- Modify: `fasolt.Server/Application/Services/CardService.cs`
- Modify: `fasolt.Server/Application/Services/DeckService.cs`
- Modify: `fasolt.Server/Application/Services/OverviewService.cs`
- Modify: `fasolt.Server/Application/Services/DueCardSummary.cs`

- [ ] **Step 1: Update ReviewService**

In `fasolt.Server/Application/Services/ReviewService.cs`:

Line 44 — add card suspension filter and invert deck logic:
```csharp
query = query.Where(c => !c.IsSuspended);
query = query.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));
```

Line 50 — invert deck check:
```csharp
if (deck.IsSuspended) return [];
```

Lines 101 — update stats filter:
```csharp
.Where(c => !c.IsSuspended)
.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));
```

- [ ] **Step 2: Update CardService**

In `fasolt.Server/Application/Services/CardService.cs`:

Add `SetSuspended` method after `ResetProgress` (around line 407):

```csharp
public async Task<CardDto?> SetSuspended(string userId, string publicId, bool isSuspended)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

    if (card is null) return null;

    card.IsSuspended = isSuspended;
    await db.SaveChangesAsync();

    return ToDto(card);
}
```

Update `ToDto` method (line 409-413) to include `IsSuspended`:
```csharp
private static CardDto ToDto(Card c) =>
    new(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
        c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsSuspended)).ToList(),
        c.IsSuspended,
        c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
        c.FrontSvg, c.BackSvg);
```

Update `BulkCreateCards` method — the inline DTO construction at lines 164-171:
```csharp
var createdDtos = created.Select(c => new CardDto(
    c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back,
    c.State, c.CreatedAt,
    deckId is not null
        ? [new CardDeckInfoDto(deckId, "", false)]
        : [],
    c.IsSuspended,
    c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
    c.FrontSvg, c.BackSvg)).ToList();
```

Update `ListCards` method — the Select projection at lines 206-209:
```csharp
.Select(c => new CardDto(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
    c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsSuspended)).ToList(),
    c.IsSuspended,
    c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
    c.FrontSvg, c.BackSvg))
```

- [ ] **Step 3: Update DeckService**

In `fasolt.Server/Application/Services/DeckService.cs`, rename all `IsActive` references to `IsSuspended` throughout. Key changes:

Line 26 (CreateDeck return):
```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, 0, 0, deck.CreatedAt, deck.IsSuspended);
```

Line 43 (ListDecks Select):
```csharp
d.IsSuspended))
```

Line 64 (GetDeck return):
```csharp
return new DeckDetailDto(deck.PublicId, deck.Name, deck.Description, cards.Count, dueCount, cards, deck.IsSuspended);
```

Update `GetDeck` cards Select (line 59) to include `IsSuspended`:
```csharp
.Select(dc => new DeckCardDto(dc.Card.PublicId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt, dc.Card.IsSuspended, dc.Card.Stability, dc.Card.Difficulty, dc.Card.Step, dc.Card.LastReviewedAt, dc.Card.FrontSvg, dc.Card.BackSvg))
```

Line 83 (UpdateDeck return):
```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt, deck.IsSuspended);
```

Lines 86-101 — rename `SetActive` to `SetSuspended`:
```csharp
public async Task<DeckDto?> SetSuspended(string userId, string publicId, bool isSuspended)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

    if (deck is null) return null;

    deck.IsSuspended = isSuspended;
    await db.SaveChangesAsync();

    var now = DateTimeOffset.UtcNow;
    var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
    var dueCount = await db.DeckCards.CountAsync(dc =>
        dc.DeckId == deck.Id && (dc.Card.DueAt == null || dc.Card.DueAt <= now));

    return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt, deck.IsSuspended);
}
```

- [ ] **Step 4: Update OverviewService**

In `fasolt.Server/Application/Services/OverviewService.cs`, line 18:
```csharp
.Where(c => !c.IsSuspended)
.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));
```

- [ ] **Step 5: Update DueCardSummary**

In `fasolt.Server/Application/Services/DueCardSummary.cs`, line 15:
```csharp
.Where(c => !c.IsSuspended)
.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended))
```

- [ ] **Step 6: Update DevSeedData**

In `fasolt.Server/Infrastructure/Data/DevSeedData.cs`, change all `IsActive = true` to remove (since `IsSuspended = false` is the default), and change `IsActive = false` (archivedDeck, line 81) to `IsSuspended = true`. Specifically:

- Line 59: remove `IsActive = true,` from capitalsDeck
- Line 70: remove `IsActive = true,` from programmingDeck
- Line 81: change `IsActive = false,` to `IsSuspended = true,` on archivedDeck
- Line 324: remove `IsActive = true,` from markdownDeck
- Line 497: remove `IsActive = true,` from mathDeck

- [ ] **Step 7: Build and verify**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat: update services and seed data for IsSuspended (#45)"
```

---

### Task 4: Backend — API Endpoints

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`

- [ ] **Step 1: Add suspend endpoint to CardEndpoints**

In `fasolt.Server/Api/Endpoints/CardEndpoints.cs`, add to the route mapping (after line 21):
```csharp
group.MapPut("/{id}/suspended", SetSuspended);
```

Add the handler method:
```csharp
private static async Task<IResult> SetSuspended(
    string id,
    SetCardSuspendedRequest request,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    CardService cardService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var dto = await cardService.SetSuspended(user.Id, id, request.IsSuspended);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}
```

Add the request record at the bottom of `CardDtos.cs`:
```csharp
public record SetCardSuspendedRequest(bool IsSuspended);
```

- [ ] **Step 2: Update DeckEndpoints**

In `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`:

Line 22 — rename route:
```csharp
group.MapPut("/{id}/suspended", SetSuspended);
```

Lines 140-152 — update handler:
```csharp
private static async Task<IResult> SetSuspended(
    string id,
    SetDeckSuspendedRequest request,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    DeckService deckService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var dto = await deckService.SetSuspended(user.Id, id, request.IsSuspended);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}
```

- [ ] **Step 3: Build and verify**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add card suspend endpoint, rename deck endpoint (#45)"
```

---

### Task 5: Backend — MCP Tools

**Files:**
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs`
- Modify: `fasolt.Server/Api/McpTools/DeckTools.cs`

- [ ] **Step 1: Update DeckTools**

In `fasolt.Server/Api/McpTools/DeckTools.cs`, rename `SetDeckActive` tool (lines 90-100):

```csharp
[McpServerTool, Description("Suspend or unsuspend a deck. Suspended decks and their cards are excluded from study/review. Cards in multiple decks remain active if at least one deck is not suspended.")]
public async Task<string> SetDeckSuspended(
    [Description("ID of the deck")] string deckId,
    [Description("true to suspend (exclude from study), false to unsuspend")] bool isSuspended)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);
    var result = await deckService.SetSuspended(userId, deckId, isSuspended);
    if (result is null)
        return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);
    return JsonSerializer.Serialize(result, McpJson.Options);
}
```

- [ ] **Step 2: Add SuspendCard tool to CardTools**

In `fasolt.Server/Api/McpTools/CardTools.cs`, add after the `AddSvgToCard` method:

```csharp
[McpServerTool, Description("Suspend or unsuspend a card. Suspended cards are excluded from study/review but retain their SRS history. Use this when a user wants to temporarily stop seeing a card.")]
public async Task<string> SuspendCard(
    [Description("Card ID")] string cardId,
    [Description("true to suspend (exclude from study), false to unsuspend")] bool isSuspended)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);
    var result = await cardService.SetSuspended(userId, cardId, isSuspended);
    if (result is null)
        return JsonSerializer.Serialize(new { error = "Card not found" }, McpJson.Options);
    return JsonSerializer.Serialize(result, McpJson.Options);
}
```

- [ ] **Step 3: Build and verify**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: update MCP tools for suspend terminology (#45)"
```

---

### Task 6: Backend — Tests

**Files:**
- Modify/create test files in `fasolt.Tests/`

- [ ] **Step 1: Run existing tests to establish baseline**

```bash
dotnet test fasolt.Tests/
```

Fix any compilation errors from the rename (update test files that reference `IsActive` to use `IsSuspended`).

- [ ] **Step 2: Commit fixes**

```bash
git add -A && git commit -m "fix: update tests for IsSuspended rename (#45)"
```

---

### Task 7: Web Frontend — Types and Stores

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Modify: `fasolt.client/src/stores/cards.ts`
- Modify: `fasolt.client/src/stores/decks.ts`
- Modify: `fasolt.client/src/stores/review.ts`

- [ ] **Step 1: Update TypeScript types**

In `fasolt.client/src/types/index.ts`:

Add `isSuspended` to `Card` interface (after line 15, before `decks`):
```typescript
isSuspended: boolean
```

Change `decks` type on Card (line 16):
```typescript
decks: { id: string; name: string; isSuspended: boolean }[]
```

Change `Deck` interface — rename `isActive` (line 34) to `isSuspended`:
```typescript
isSuspended: boolean
```

Add `isSuspended` to `DeckCard` interface (after `dueAt`):
```typescript
isSuspended: boolean
```

- [ ] **Step 2: Add setSuspended to cards store**

In `fasolt.client/src/stores/cards.ts`, add after `resetProgress`:

```typescript
async function setSuspended(id: string, isSuspended: boolean): Promise<Card> {
  const result = await apiFetch<Card>(`/cards/${id}/suspended`, {
    method: 'PUT',
    body: JSON.stringify({ isSuspended }),
  })
  const idx = cards.value.findIndex(c => c.id === id)
  if (idx !== -1) cards.value[idx] = result
  return result
}
```

Add `setSuspended` to the return statement.

- [ ] **Step 3: Update decks store**

In `fasolt.client/src/stores/decks.ts`, rename `setActive` to `setSuspended` (lines 59-67):

```typescript
async function setSuspended(id: string, isSuspended: boolean): Promise<Deck> {
  const result = await apiFetch<Deck>(`/decks/${id}/suspended`, {
    method: 'PUT',
    body: JSON.stringify({ isSuspended }),
  })
  const idx = decks.value.findIndex(d => d.id === id)
  if (idx !== -1) decks.value[idx] = result
  return result
}
```

Update the return statement to export `setSuspended` instead of `setActive`.

- [ ] **Step 4: Add suspend action to review store**

In `fasolt.client/src/stores/review.ts`, add `suspended` counter to sessionStats (line 14):

```typescript
const sessionStats = ref({
  reviewed: 0,
  again: 0,
  hard: 0,
  good: 0,
  easy: 0,
  skipped: 0,
  suspended: 0,
  startTime: 0,
})
```

Update `startSession` reset (line 51):
```typescript
sessionStats.value = { reviewed: 0, again: 0, hard: 0, good: 0, easy: 0, skipped: 0, suspended: 0, startTime: Date.now() }
```

Add `suspend` function after `skip`:

```typescript
async function suspend() {
  const card = currentCard.value
  if (!card) return
  try {
    await apiFetch(`/cards/${card.id}/suspended`, {
      method: 'PUT',
      body: JSON.stringify({ isSuspended: true }),
    })
  } catch {
    // best-effort — card still skipped even if API fails
  }
  sessionStats.value.suspended++
  currentIndex.value++
  isFlipped.value = false
}
```

Add `suspend` to the return statement.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(web): update types and stores for suspend (#45)"
```

---

### Task 8: Web Frontend — Views

**Files:**
- Modify: `fasolt.client/src/views/CardsView.vue`
- Modify: `fasolt.client/src/views/DecksView.vue`
- Modify: `fasolt.client/src/views/DeckDetailView.vue`
- Modify: `fasolt.client/src/views/StudyView.vue`
- Modify: `fasolt.client/src/views/ReviewView.vue`
- Modify: `fasolt.client/src/components/CardTable.vue`

- [ ] **Step 1: Update CardsView**

In `fasolt.client/src/views/CardsView.vue`:

Rename `activeOnly` ref (line 31) to something clearer for suspended:
```typescript
const hideSuspended = ref(true)
```

Update the filter logic (lines 64-72). Replace the active filter block:
```typescript
// Hide suspended cards (card-level suspension)
if (hideSuspended.value) {
  result = result.filter(card => !card.isSuspended)
}
```

Update the checkbox label (line 139-142):
```html
<label class="flex items-center gap-1.5 text-xs cursor-pointer">
  <input type="checkbox" v-model="hideSuspended" class="rounded border-border" />
  Hide suspended
</label>
```

- [ ] **Step 2: Update CardTable**

In `fasolt.client/src/components/CardTable.vue`:

Add `suspend` emit (line 34-38):
```typescript
const emit = defineEmits<{
  delete: [card: any]
  remove: [card: any]
  addToDeck: [card: any]
  suspend: [card: any]
}>()
```

Add a Suspend/Unsuspend button in the actions column (in the cell function around lines 103-117). Insert before the delete button:
```typescript
buttons.push(
  h(Button, {
    variant: 'ghost',
    size: 'sm',
    class: 'h-6 text-[10px]',
    onClick: () => emit('suspend', card),
  }, () => card.isSuspended ? 'Unsuspend' : 'Suspend'),
)
```

Add suspended styling to the table row (line 167). Change:
```html
<TableRow v-for="row in table.getRowModel().rows" :key="row.id" class="text-xs hover:bg-accent/5">
```
to:
```html
<TableRow v-for="row in table.getRowModel().rows" :key="row.id" :class="['text-xs hover:bg-accent/5', row.original.isSuspended && 'opacity-50']">
```

- [ ] **Step 3: Wire suspend in CardsView**

In `fasolt.client/src/views/CardsView.vue`, add the store import and handler. Add to the imports:
```typescript
import { useCardsStore } from '@/stores/cards'
```

(already imported). Add handler function:
```typescript
async function onSuspend(card: Card) {
  await cardsStore.setSuspended(card.id, !card.isSuspended)
}
```

Update the CardTable usage (around line 146-154) to add the suspend handler:
```html
<CardTable
  :cards="filteredCards"
  show-decks
  show-pagination
  @delete="(card) => { deleteTarget = card; deleteOpen = true }"
  @add-to-deck="(card) => addToDeckCard = card"
  @suspend="onSuspend"
>
```

- [ ] **Step 4: Update DecksView**

In `fasolt.client/src/views/DecksView.vue`:

Line 22-27 — update `sortedDecks` to sort by `isSuspended`:
```typescript
const sortedDecks = computed(() =>
  [...decksStore.decks].sort((a, b) => {
    if (a.isSuspended !== b.isSuspended) return a.isSuspended ? 1 : -1
    return a.name.localeCompare(b.name)
  })
)
```

Line 75 — update opacity class:
```
deck.isSuspended
```

Line 88 — update badge text from "Inactive" to "Suspended":
```
Suspended
```

- [ ] **Step 5: Update DeckDetailView**

In `fasolt.client/src/views/DeckDetailView.vue`:

Line 83-87 — update `toggleActive`:
```typescript
async function toggleSuspended() {
  if (!deck.value) return
  await decks.setSuspended(deck.value.id, !deck.value.isSuspended)
  deck.value = await decks.getDeckDetail(deck.value.id)
}
```

Line 131 — update button text:
```
deck.isSuspended ? 'Unsuspend' : 'Suspend'
```

Update the button's `@click` to call `toggleSuspended`.

Line 123-129 — update study button condition:
```
!deck.isSuspended
```

Lines 139-141 — update inactive banner:
```
deck.isSuspended
```
And change text to "Suspended" instead of "Inactive".

- [ ] **Step 6: Update StudyView**

In `fasolt.client/src/views/StudyView.vue`:

Line 12 — update `activeDecks`:
```typescript
const activeDecks = computed(() => decksStore.decks.filter(d => !d.isSuspended))
```

- [ ] **Step 7: Update ReviewView**

In `fasolt.client/src/views/ReviewView.vue`:

Add suspend keyboard shortcut in onMounted (after the 's' key binding on line 30):
```typescript
'x': () => { if (!review.isComplete) review.suspend() },
```

Add "Suspend" button next to Skip. In the flipped state (around line 87), change the Skip button:
```html
<div class="mt-3 flex justify-center gap-3">
  <button class="text-xs text-muted-foreground/50 hover:text-muted-foreground transition-colors" @click="review.skip()">Skip</button>
  <button class="text-xs text-muted-foreground/50 hover:text-muted-foreground transition-colors" @click="review.suspend()">Suspend</button>
</div>
```

In the non-flipped state (around line 93-94), add suspend there too:
```html
<div class="mt-2 flex justify-center gap-3">
  <button class="text-xs text-muted-foreground/50 hover:text-muted-foreground transition-colors" @click="review.skip()">Skip</button>
  <button class="text-xs text-muted-foreground/50 hover:text-muted-foreground transition-colors" @click="review.suspend()">Suspend</button>
</div>
```

Add keyboard hint in context bar (line 63):
```html
<span class="flex items-center gap-1"><KbdHint keys="x" /> suspend</span>
```

- [ ] **Step 8: Verify frontend builds**

```bash
cd fasolt.client && npm run build
```

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat(web): add suspend UI to card table and review (#45)"
```

---

### Task 9: iOS — Suspend in Review Session

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/StudyView.swift`
- Modify: `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`
- Modify: `fasolt.ios/Fasolt/Repositories/CardRepository.swift`

- [ ] **Step 1: Add suspendCard to CardRepository**

In `fasolt.ios/Fasolt/Repositories/CardRepository.swift`, add after `rateCard`:

```swift
func suspendCard(cardId: String) async throws {
    let body = ["isSuspended": true]
    let endpoint = Endpoint(path: "/api/cards/\(cardId)/suspended", method: .put, body: body)
    let _: EmptyResponse = try await apiClient.request(endpoint)
    logger.info("Suspended card \(cardId)")
}
```

Check if `EmptyResponse` exists. If not, use a simple decodable struct or use the Card DTO. Since the endpoint returns a `CardDto`, decode it:

```swift
func suspendCard(cardId: String) async throws {
    struct SuspendRequest: Encodable {
        let isSuspended: Bool
    }
    let body = SuspendRequest(isSuspended: true)
    let endpoint = Endpoint(path: "/api/cards/\(cardId)/suspended", method: .put, body: body)
    // Response is a CardDto but we don't need it
    let _: DueCardDTO = try await apiClient.request(endpoint)
    logger.info("Suspended card \(cardId)")
}
```

Wait — `DueCardDTO` won't match the full `CardDto` response. Simpler: just ignore the response body. Check if the API client supports that or if we need a throwaway type. The safest approach is to define a minimal response type or use a raw request. Let's use a simple approach — define a tiny struct that ignores most fields:

```swift
func suspendCard(cardId: String) async throws {
    struct SuspendBody: Encodable { let isSuspended = true }
    struct SuspendResponse: Decodable { let id: String }
    let endpoint = Endpoint(path: "/api/cards/\(cardId)/suspended", method: .put, body: SuspendBody())
    let _ = try await apiClient.request(endpoint) as SuspendResponse
    logger.info("Suspended card \(cardId)")
}
```

- [ ] **Step 2: Add suspendCard to StudyViewModel**

In `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`, add a `suspendedCount` property (after `skippedCount`, line 19):
```swift
var suspendedCount: Int = 0
```

Reset it in `startSession` (around line 59):
```swift
suspendedCount = 0
```

Add `suspendCard` method after `skipCard`:
```swift
func suspendCard() async {
    guard let card = currentCard else { return }
    do {
        try await cardRepository.suspendCard(cardId: card.id)
    } catch {
        // best-effort — still skip the card
    }
    suspendedCount += 1
    currentIndex += 1
    isFlipped = false
    if currentIndex >= cards.count {
        state = .summary
        if cardsStudied > 0 {
            requestNotificationPermissionIfNeeded()
        }
    } else {
        state = .studying
    }
}
```

- [ ] **Step 3: Add Suspend button to StudyView toolbar**

In `fasolt.ios/Fasolt/Views/Study/StudyView.swift`, update the toolbar trailing item (lines 48-63). Replace the single Skip button with a menu or add a second button. Since toolbar space is limited, use a Menu:

Replace the `ToolbarItem(placement: .topBarTrailing)` block:
```swift
ToolbarItem(placement: .topBarTrailing) {
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
```

- [ ] **Step 4: Build iOS project**

```bash
cd fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(ios): add suspend button to review session (#45)"
```

---

### Task 10: Playwright Testing

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh &
```

Wait for backend and frontend to be ready.

- [ ] **Step 2: Test card suspension from card table**

Using Playwright MCP:
1. Navigate to the app login page
2. Log in with dev@fasolt.local / Dev1234!
3. Navigate to Cards view
4. Find a card and click Suspend
5. Verify the card appears dimmed (opacity-50)
6. Verify "Hide suspended" checkbox hides it
7. Uncheck "Hide suspended", find the card, click Unsuspend
8. Verify it returns to normal opacity

- [ ] **Step 3: Test card suspension from review**

1. Navigate to /study
2. Start a review session
3. Click Suspend on a card
4. Verify the card advances to the next one
5. Complete or end the session
6. Start a new session — verify the suspended card doesn't appear

- [ ] **Step 4: Test deck suspension**

1. Navigate to Decks
2. Click on a deck
3. Click Suspend
4. Verify the deck shows "Suspended" badge
5. Verify Study view doesn't show the suspended deck
6. Unsuspend the deck and verify it reappears
