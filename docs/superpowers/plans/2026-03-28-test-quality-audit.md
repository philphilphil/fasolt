# Test Quality Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Audit and improve all backend and frontend tests — delete noise, fix weak assertions, add missing coverage.

**Architecture:** Test-only changes across two areas: .NET integration tests (`fasolt.Tests/`) and Vue/Vitest unit tests (`fasolt.client/src/__tests__/`). No production code changes.

**Tech Stack:** .NET 10 + xUnit + FluentAssertions (backend), Vitest + Pinia + vue-test-utils (frontend)

---

### Task 1: Delete noise backend tests

**Files:**
- Delete: `fasolt.Tests/NotificationTests.cs`
- Delete: `fasolt.Tests/ReviewTests.cs`

- [ ] **Step 1: Delete NotificationTests.cs**

Delete the entire file `fasolt.Tests/NotificationTests.cs`. This file contains 13 tests that duplicate `DeviceTokenServiceTests` with worse implementation and query tests that don't verify actual service behavior.

- [ ] **Step 2: Move ReviewTests content into ReviewServiceTests.cs**

The single test `NewCard_AppearsDue` in `ReviewTests.cs` tests `GetDueCards` + source metadata — this is already covered more thoroughly by `ReviewServiceTests.GetDueCards_ReturnsNewCards`. Delete the file.

Delete `fasolt.Tests/ReviewTests.cs`.

- [ ] **Step 3: Run backend tests to verify nothing broke**

Run: `dotnet test fasolt.Tests/`
Expected: All remaining tests pass. Test count drops by 14 (13 NotificationTests + 1 ReviewTests).

- [ ] **Step 4: Commit**

```bash
git add -A fasolt.Tests/NotificationTests.cs fasolt.Tests/ReviewTests.cs
git commit -m "test: remove noise tests (NotificationTests, ReviewTests)"
```

---

### Task 2: Fix SearchServiceTests assertions

**Files:**
- Modify: `fasolt.Tests/SearchServiceTests.cs`

- [ ] **Step 1: Strengthen Search_FindsCards_ByFrontText**

Replace the weak assertion that just checks `Contain` with assertions that verify filtering actually works — assert result count and that non-matching cards are excluded.

Replace the test at lines 29-40:

```csharp
[Fact]
public async Task Search_FindsCards_ByFrontText()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var searchSvc = new SearchService(db);

    await cardSvc.CreateCard(UserId, "Photosynthesis question", "It makes food from light.", null, null);
    await cardSvc.CreateCard(UserId, "Unrelated card", "Nothing about plants.", null, null);

    var result = await searchSvc.Search(UserId, "Photosynthesis");

    result.Cards.Should().ContainSingle();
    result.Cards[0].Headline.Should().Contain("Photosynthesis");
}
```

- [ ] **Step 2: Strengthen Search_FindsDecks_ByName**

Replace the test at lines 43-54:

```csharp
[Fact]
public async Task Search_FindsDecks_ByName()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var searchSvc = new SearchService(db);

    await deckSvc.CreateDeck(UserId, "Biology Fundamentals", "Core biology concepts");
    await deckSvc.CreateDeck(UserId, "Mathematics", "Numbers and proofs");

    var result = await searchSvc.Search(UserId, "Biology");

    result.Decks.Should().ContainSingle();
    result.Decks[0].Headline.Should().Contain("Biology");
}
```

- [ ] **Step 3: Remove Search_ResponseHasNoFilesProperty noise test**

Delete the `Search_ResponseHasNoFilesProperty` test (lines 86-97). This is a DTO shape test using reflection — the compiler already enforces this.

- [ ] **Step 4: Run backend tests to verify**

Run: `dotnet test fasolt.Tests/`
Expected: All tests pass. SearchServiceTests count drops from 7 to 6.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Tests/SearchServiceTests.cs
git commit -m "test: strengthen search assertions, remove DTO shape test"
```

---

### Task 3: Delete noise frontend tests

**Files:**
- Delete: `fasolt.client/src/__tests__/types.test.ts`
- Delete: `fasolt.client/src/__tests__/useDarkMode.test.ts`

- [ ] **Step 1: Delete types.test.ts**

Delete `fasolt.client/src/__tests__/types.test.ts`. These 8 tests check TypeScript compile-time guarantees at runtime (e.g., "Card does NOT have fileId"). TypeScript already enforces this.

- [ ] **Step 2: Delete useDarkMode.test.ts**

Delete `fasolt.client/src/__tests__/useDarkMode.test.ts`. The single test asserts the opposite of its name and provides zero signal.

- [ ] **Step 3: Run frontend tests to verify**

Run: `cd fasolt.client && npx vitest run`
Expected: All remaining tests pass. Test count drops by 9 (8 types + 1 useDarkMode).

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/__tests__/types.test.ts fasolt.client/src/__tests__/useDarkMode.test.ts
git commit -m "test: remove noise frontend tests (types, useDarkMode)"
```

---

### Task 4: Add missing decks store tests

**Files:**
- Modify: `fasolt.client/src/__tests__/stores/decks.test.ts`

- [ ] **Step 1: Rewrite decks.test.ts with full coverage**

Replace the entire file content. Remove the "addFileCards method does NOT exist" noise test. Add tests for createDeck, updateDeck, and deleteDeck that verify API calls and store state changes.

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useDecksStore } from '@/stores/decks'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

function makeDeck(overrides: Partial<{ id: string; name: string; description: string | null; cardCount: number; dueCount: number }> = {}) {
  return {
    id: 'd1',
    name: 'Test Deck',
    description: null,
    cardCount: 0,
    dueCount: 0,
    createdAt: '2024-01-01T00:00:00Z',
    ...overrides,
  }
}

async function seedStore(store: ReturnType<typeof useDecksStore>, decks: ReturnType<typeof makeDeck>[]) {
  mockApiFetch.mockResolvedValueOnce(decks)
  await store.fetchDecks()
  vi.clearAllMocks()
}

describe('decks store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('has empty initial state', () => {
    const store = useDecksStore()
    expect(store.decks).toEqual([])
  })

  it('fetchDecks calls /decks and populates store', async () => {
    const mockDecks = [makeDeck({ id: 'd1', name: 'Biology' })]
    mockApiFetch.mockResolvedValueOnce(mockDecks)

    const store = useDecksStore()
    await store.fetchDecks()

    expect(mockApiFetch).toHaveBeenCalledWith('/decks')
    expect(store.decks).toEqual(mockDecks)
  })

  it('createDeck sends POST and adds deck to store', async () => {
    const newDeck = makeDeck({ id: 'd1', name: 'Biology', description: 'Bio concepts' })
    mockApiFetch.mockResolvedValueOnce(newDeck)

    const store = useDecksStore()
    const result = await store.createDeck('Biology', 'Bio concepts')

    expect(mockApiFetch).toHaveBeenCalledWith('/decks', {
      method: 'POST',
      body: JSON.stringify({ name: 'Biology', description: 'Bio concepts' }),
    })
    expect(result.name).toBe('Biology')
    expect(store.decks).toHaveLength(1)
    expect(store.decks[0].id).toBe('d1')
  })

  it('updateDeck sends PUT and updates deck in store', async () => {
    const store = useDecksStore()
    await seedStore(store, [makeDeck({ id: 'd1', name: 'Old Name' })])

    const updated = makeDeck({ id: 'd1', name: 'New Name' })
    mockApiFetch.mockResolvedValueOnce(updated)
    await store.updateDeck('d1', 'New Name')

    expect(mockApiFetch).toHaveBeenCalledWith('/decks/d1', {
      method: 'PUT',
      body: JSON.stringify({ name: 'New Name', description: undefined }),
    })
    expect(store.decks[0].name).toBe('New Name')
  })

  it('deleteDeck sends DELETE and removes deck from store', async () => {
    const store = useDecksStore()
    await seedStore(store, [makeDeck({ id: 'd1' }), makeDeck({ id: 'd2', name: 'Other' })])

    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteDeck('d1')

    expect(mockApiFetch).toHaveBeenCalledWith('/decks/d1', { method: 'DELETE' })
    expect(store.decks).toHaveLength(1)
    expect(store.decks[0].id).toBe('d2')
  })

  it('deleteDeck with deleteCards passes query param', async () => {
    const store = useDecksStore()
    await seedStore(store, [makeDeck({ id: 'd1' })])

    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteDeck('d1', true)

    expect(mockApiFetch).toHaveBeenCalledWith('/decks/d1?deleteCards=true', { method: 'DELETE' })
  })
})
```

- [ ] **Step 2: Run frontend tests to verify**

Run: `cd fasolt.client && npx vitest run`
Expected: All tests pass. decks.test.ts now has 6 meaningful tests.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/__tests__/stores/decks.test.ts
git commit -m "test: rewrite decks store tests with full CRUD coverage"
```

---

### Task 5: Add missing review store tests

**Files:**
- Modify: `fasolt.client/src/__tests__/stores/review.test.ts`

- [ ] **Step 1: Add edge case and error tests**

Add these tests to the existing `describe('review store')` block at the end of the file:

```typescript
  it('startSession with no due cards sets noDueCards', async () => {
    mockApiFetch.mockResolvedValueOnce([])

    const store = useReviewStore()
    await store.startSession()

    expect(store.isActive).toBe(true)
    expect(store.noDueCards).toBe(true)
    expect(store.currentCard).toBeNull()
  })

  it('startSession sets error on API failure', async () => {
    mockApiFetch.mockRejectedValueOnce(new Error('Network error'))

    const store = useReviewStore()
    await store.startSession()

    expect(store.error).toBe('Failed to load review session. Please try again.')
    expect(store.loading).toBe(false)
  })

  it('rate sends correct API request', async () => {
    mockApiFetch.mockResolvedValueOnce([
      { id: 'c1', front: 'Q', back: 'A', sourceFile: null, sourceHeading: null, state: 'new', frontSvg: null, backSvg: null },
    ])
    mockApiFetch.mockResolvedValueOnce({ cardId: 'c1' })

    const store = useReviewStore()
    await store.startSession()
    vi.clearAllMocks()

    mockApiFetch.mockResolvedValueOnce({ cardId: 'c1' })
    store.flipCard()
    await store.rate('hard')

    expect(mockApiFetch).toHaveBeenCalledWith('/review/rate', {
      method: 'POST',
      body: JSON.stringify({ cardId: 'c1', rating: 'hard' }),
    })
    expect(store.sessionStats.hard).toBe(1)
    expect(store.sessionStats.reviewed).toBe(1)
  })

  it('rate again re-enqueues the card', async () => {
    mockApiFetch.mockResolvedValueOnce([
      { id: 'c1', front: 'Q', back: 'A', sourceFile: null, sourceHeading: null, state: 'new', frontSvg: null, backSvg: null },
    ])

    const store = useReviewStore()
    await store.startSession()

    mockApiFetch.mockResolvedValueOnce({ cardId: 'c1' })
    store.flipCard()
    await store.rate('again')

    expect(store.queue).toHaveLength(2)
    expect(store.sessionStats.again).toBe(1)
  })
```

- [ ] **Step 2: Run frontend tests to verify**

Run: `cd fasolt.client && npx vitest run`
Expected: All tests pass. review.test.ts now has 8 tests.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/__tests__/stores/review.test.ts
git commit -m "test: add review store edge case and error tests"
```

---

### Task 6: Final verification

- [ ] **Step 1: Run all backend tests**

Run: `dotnet test fasolt.Tests/`
Expected: All tests pass.

- [ ] **Step 2: Run all frontend tests**

Run: `cd fasolt.client && npx vitest run`
Expected: All tests pass.

- [ ] **Step 3: Final commit if any cleanup needed**

Only commit if there are uncommitted changes from fixing test failures.
