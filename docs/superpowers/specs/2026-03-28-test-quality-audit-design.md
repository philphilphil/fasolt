# Test Quality Audit (#49)

## Summary

Audit all backend (.NET) and frontend (Vue/Vitest) tests for quality and correctness. Delete noise tests, fix weak assertions, add missing coverage. iOS tests are out of scope.

## Deletions

### `fasolt.Tests/NotificationTests.cs` — Delete entire file (13 tests)

Duplicates `DeviceTokenServiceTests` with worse implementation (direct DB manipulation instead of service calls). Query tests verify SQL structure but not that the notification service actually uses the queries. `DeviceTokenServiceTests` already covers device token CRUD properly.

### `fasolt.client/src/__tests__/types.test.ts` — Delete entire file (8 tests)

Runtime assertions for TypeScript compile-time guarantees. Checks like "Card does NOT have fileId" are enforced by the type system. These were migration regression guards with no ongoing value.

### `fasolt.client/src/__tests__/useDarkMode.test.ts` — Delete entire file (1 test)

Single test named "adds dark class when system prefers dark" that asserts the dark class is NOT present. Contradicts its own name and provides zero signal.

## Fixes

### `fasolt.Tests/SearchServiceTests.cs` — Strengthen assertions, remove noise

**Problem:** Assertions use `Should().NotBeEmpty()` without verifying search actually filtered correctly. Tests would pass even if search returned all cards unfiltered.

**Fix:**
- Assert result count matches expected matches
- Assert returned cards actually match the search term
- Assert non-matching cards are excluded from results
- Remove `Search_ResponseHasNoFilesProperty` test (DTO shape test, not behavior)

### `fasolt.Tests/ReviewTests.cs` — Consolidate into ReviewServiceTests

Contains 1 test. Move it into `ReviewServiceTests.cs` and delete the file to reduce fragmentation.

## Missing Coverage

### `fasolt.client/src/__tests__/stores/decks.test.ts`

Currently 3 tests (fetch, initial state, method-doesn't-exist check). The "method doesn't exist" check is the same noise pattern as types.test.ts.

**Add:**
- `createDeck` sends correct API request and updates store
- `updateDeck` calls PUT with correct endpoint and body
- `deleteDeck` calls DELETE and removes deck from store

**Remove:**
- `addFileCards method does NOT exist on decks store` test

### `fasolt.client/src/__tests__/stores/review.test.ts`

Currently 4 tests covering only the happy path.

**Add:**
- `startSession` with no due cards produces empty/complete session
- `startSession` handles API error gracefully
- `rate` sends correct API request with rating value

## Unchanged (Good Quality)

**Backend:** CardServiceTests (20), DeckServiceTests (16), ReviewServiceTests (9), SuspensionTests (6), OverviewServiceTests, SourceServiceTests, DeviceTokenServiceTests, FasoltFactory helper.

**Frontend:** cards.test.ts (18), sources.test.ts (4), useKeyboardShortcuts.test.ts (3), useSearch.test.ts (5).
