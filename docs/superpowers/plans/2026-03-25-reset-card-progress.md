# Reset Card Study Progress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a button on the card detail page to reset a single card's SRS progress, returning it to "new" state.

**Architecture:** New `POST /api/cards/{id}/reset` endpoint delegates to `CardService.ResetProgress()`, which nulls all FSRS fields and sets state to "new". Frontend adds a "Reset Progress" button with confirmation dialog on CardDetailView.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, Vue 3, Pinia, shadcn-vue

**Spec:** `docs/superpowers/specs/2026-03-25-reset-card-progress-design.md`

---

### File Map

- Modify: `fasolt.Server/Application/Services/CardService.cs` — add `ResetProgress` method
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs` — add `POST /{id}/reset` route + handler
- Modify: `fasolt.client/src/stores/cards.ts` — add `resetProgress` store method
- Modify: `fasolt.client/src/views/CardDetailView.vue` — add reset button + confirmation dialog

---

### Task 1: Backend — CardService.ResetProgress

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs`

- [ ] **Step 1: Add `ResetProgress` method to CardService**

Add this method after `DeleteCards` (around line 301):

```csharp
public async Task<CardDto?> ResetProgress(string userId, string publicId)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

    if (card is null) return null;

    card.Stability = null;
    card.Difficulty = null;
    card.Step = null;
    card.DueAt = null;
    card.State = "new";
    card.LastReviewedAt = null;

    await db.SaveChangesAsync();

    return ToDto(card);
}
```

- [ ] **Step 2: Verify backend builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs
git commit -m "feat: add CardService.ResetProgress method (#21)"
```

---

### Task 2: Backend — Reset Endpoint

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs`

- [ ] **Step 1: Register the reset route**

In `MapCardEndpoints()`, add after the `Delete` mapping (line 20):

```csharp
group.MapPost("/{id}/reset", ResetProgress);
```

- [ ] **Step 2: Add the endpoint handler**

Add after the `Delete` handler method (around line 102):

```csharp
private static async Task<IResult> ResetProgress(
    string id,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    CardService cardService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var dto = await cardService.ResetProgress(user.Id, id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}
```

- [ ] **Step 3: Verify backend builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/CardEndpoints.cs
git commit -m "feat: add POST /api/cards/{id}/reset endpoint (#21)"
```

---

### Task 3: Frontend — Cards Store Method

**Files:**
- Modify: `fasolt.client/src/stores/cards.ts`

- [ ] **Step 1: Add `resetProgress` method**

Add after the `getCard` function (line 53), before the `return` statement:

```typescript
async function resetProgress(id: string): Promise<Card> {
  const result = await apiFetch<Card>(`/cards/${id}/reset`, { method: 'POST' })
  const idx = cards.value.findIndex(c => c.id === id)
  if (idx !== -1) cards.value[idx] = result
  return result
}
```

- [ ] **Step 2: Export the new method**

Update the return statement (line 55) to include `resetProgress`:

```typescript
return { cards, loading, fetchCards, getCard, createCard, updateCard, deleteCard, resetProgress }
```

- [ ] **Step 3: Verify frontend builds**

Run: `cd fasolt.client && npx vue-tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/stores/cards.ts
git commit -m "feat: add resetProgress to cards store (#21)"
```

---

### Task 4: Frontend — Reset Button and Dialog on Card Detail Page

**Files:**
- Modify: `fasolt.client/src/views/CardDetailView.vue`

- [ ] **Step 1: Add reactive state for the reset dialog**

In the `<script setup>` section, after `const error = ref('')` (line 34), add:

```typescript
const resetOpen = ref(false)
const resetting = ref(false)
const resetSuccess = ref(false)
```

- [ ] **Step 2: Add the `confirmReset` handler**

After the `confirmDelete` function (line 92), add:

```typescript
async function confirmReset() {
  if (!card.value) return
  resetting.value = true
  try {
    card.value = await cardsStore.resetProgress(card.value.id)
    resetOpen.value = false
    resetSuccess.value = true
    setTimeout(() => resetSuccess.value = false, 2000)
  } catch {
    error.value = 'Failed to reset progress. Please try again.'
    resetOpen.value = false
  } finally {
    resetting.value = false
  }
}
```

- [ ] **Step 3: Add the Reset Progress button**

In the template, inside the header button group (after the Edit button, before the Delete button — around line 113), add:

```html
<Button
  v-if="!editing"
  variant="outline"
  size="sm"
  class="h-7 text-[10px] text-destructive hover:text-destructive"
  @click="resetOpen = true"
>
  Reset Progress
</Button>
```

- [ ] **Step 4: Add success feedback message**

In the template, after the SRS Stats `</div>` (the `bg-secondary rounded-lg` block, around line 162), add:

```html
<div v-if="resetSuccess" class="text-xs text-green-600 dark:text-green-400">Progress reset.</div>
```

- [ ] **Step 5: Add the confirmation dialog**

After the existing delete confirmation dialog (after line 253, before `</div>`), add:

```html
<Dialog v-model:open="resetOpen">
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Reset study progress</DialogTitle>
      <DialogDescription>
        This will clear all SRS data (stability, difficulty, scheduling) and return the card to "new" state.
      </DialogDescription>
    </DialogHeader>
    <DialogFooter>
      <Button variant="outline" @click="resetOpen = false">Cancel</Button>
      <Button variant="destructive" :disabled="resetting" @click="confirmReset">
        {{ resetting ? 'Resetting...' : 'Reset' }}
      </Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

- [ ] **Step 6: Verify frontend builds**

Run: `cd fasolt.client && npx vue-tsc --noEmit`
Expected: No errors

- [ ] **Step 7: Commit**

```bash
git add fasolt.client/src/views/CardDetailView.vue
git commit -m "feat: add reset progress button with confirmation dialog (#21)"
```

---

### Task 5: End-to-End Testing with Playwright

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh` (or ensure backend + frontend + Postgres are running)

- [ ] **Step 2: Test the reset flow in the browser**

Using Playwright MCP:
1. Navigate to `http://localhost:5173/login`
2. Log in with `dev@fasolt.local` / `Dev1234!`
3. Navigate to a card detail page (pick any card, or create one first)
4. If the card is in "new" state, study it first via `/review` to give it SRS data
5. Return to the card detail page
6. Verify SRS stats panel shows non-null values (stability, difficulty, etc.)
7. Click "Reset Progress" button
8. Verify the confirmation dialog appears with correct title and description
9. Click "Reset"
10. Verify SRS stats panel now shows: State = "new", and dashes for stability, difficulty, step, last review, due

- [ ] **Step 3: Test cancel flow**

1. Click "Reset Progress" again
2. Click "Cancel"
3. Verify dialog closes, card state remains "new" (no double-reset error)

- [ ] **Step 4: Commit test results and close issue**

```bash
gh issue close 21
```
