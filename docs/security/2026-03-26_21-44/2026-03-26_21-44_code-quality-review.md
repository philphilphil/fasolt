# Code Quality Review — Findings

## Critical 🔴

No findings.

---

## High 🟠

### `QUAL-H001` — Silent error swallowing in `CardsView.vue` addCardToDeck
- **ID:** `QUAL-H001`
- **File:** `fasolt.client/src/views/CardsView.vue:L52`
- **Risk:** The `addCardToDeck` function catches all errors silently (`// silently fail`). If the API call fails (network error, auth expired, deck not found), the user receives zero feedback. The card appears to be added but isn't, leading to a confusing state where the UI says it succeeded but it didn't.
- **Evidence:**
  ```typescript
  async function addCardToDeck() {
    if (!addToDeckCard.value || !addToDeckId.value) return
    try {
      await decks.addCards(addToDeckId.value, [addToDeckCard.value.id])
      await cardsStore.fetchCards()
      addToDeckCard.value = null
      addToDeckId.value = ''
    } catch {
      // silently fail
    }
  }
  ```
- **Fix:** Display an error message to the user, similar to other views:
  ```typescript
  } catch {
    // show error to user
    errorMessage.value = 'Failed to add card to deck.'
  }
  ```

---

### `QUAL-H002` — `apiFetch` returns `undefined as T` for empty responses, violating type safety
- **ID:** `QUAL-H002`
- **File:** `fasolt.client/src/api/client.ts:L36`
- **Risk:** When the API returns a 2xx with no body (e.g., 204 No Content from DELETE), the function returns `undefined as T`. This is an unsafe cast that defeats TypeScript's type system. Callers that expect `T` (e.g., `apiFetch<Card>(...)`) may dereference `undefined`, causing runtime errors.
- **Evidence:**
  ```typescript
  const text = await response.text()
  if (!text) return undefined as T
  ```
- **Fix:** Use a separate function signature for void-returning endpoints, or use a discriminated return type. At minimum, callers of DELETE/POST-no-body should use `apiFetch<void>(...)` and the function should have an overload:
  ```typescript
  export async function apiFetch(path: string, options?: RequestInit): Promise<void>
  export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T>
  export async function apiFetch<T = void>(path: string, options?: RequestInit): Promise<T | void> {
    // ...
    if (!text) return undefined
    return JSON.parse(text) as T
  }
  ```

---

### `QUAL-H003` — Duplicated due-card notification query between AdminEndpoints and NotificationBackgroundService
- **ID:** `QUAL-H003`
- **File:** `fasolt.Server/Api/Endpoints/AdminEndpoints.cs:L116` and `fasolt.Server/Infrastructure/Services/NotificationBackgroundService.cs:L124`
- **Risk:** The same complex LINQ query (due cards grouped by deck with breakdown string) is duplicated in two places. The code comment in AdminEndpoints even says "same query as the background service." If the due-card logic changes (e.g., filtering rules, deck active check), one copy may be updated while the other is forgotten, leading to inconsistent notification content.
- **Evidence:**
  ```csharp
  // AdminEndpoints.cs:L115-123
  var dueCardsByDeck = await db.Cards
      .Where(c => c.UserId == id && (c.DueAt == null || c.DueAt <= now))
      .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive))
      .SelectMany(c => c.DeckCards.DefaultIfEmpty(), ...)
      .GroupBy(...)
      .Select(...)
      .ToListAsync();

  // NotificationBackgroundService.cs:L124-131 — identical query
  ```
- **Fix:** Extract the due-card-by-deck query and body formatting into a shared method (e.g., on a `NotificationHelper` or a shared service method) so both call sites use the same logic.

---

## Medium 🟡

### `QUAL-M001` — Duplicated ClaimsIdentity + SetDestinations block in OAuthEndpoints
- **ID:** `QUAL-M001`
- **File:** `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:L158-178` and `L204-225`
- **Risk:** The authorization and token endpoints both build a `ClaimsIdentity` with the same claims, scopes, and destination mapping. The `SetDestinations` lambda is duplicated verbatim. Any divergence between these two blocks would cause inconsistent token claims.
- **Evidence:** Two identical `identity.SetDestinations(static claim => claim.Type switch { ... })` blocks at L168 and L214.
- **Fix:** Extract a `BuildClaimsIdentity(userId, userName, scopes)` helper method.

---

### `QUAL-M002` — `CardTable.vue` uses `any` types throughout, losing type safety
- **ID:** `QUAL-M002`
- **File:** `fasolt.client/src/components/CardTable.vue:L24,L30,L35-37`
- **Risk:** The `cards` prop is typed as `any[]`, and all emits use `any`. This is a widely-used shared component, so the loss of type safety propagates to every consumer. Bugs from mismatched property names or missing fields won't be caught at compile time.
- **Evidence:**
  ```typescript
  const props = withDefaults(defineProps<{
    cards: any[]  // should be Card[] or DeckCard[]
    // ...
  }>(), { ... })

  const emit = defineEmits<{
    delete: [card: any]   // should be typed
    remove: [card: any]
    addToDeck: [card: any]
  }>()
  ```
- **Fix:** Define a union type or shared interface for the card shape used in the table, e.g., `Card | DeckDetailCard`, and type the props and emits accordingly. Also fix `DeckDetailView.vue:L30` where `deleteCardTarget = ref<any>(null)`.

---

### `QUAL-M003` — Repeated `UserManager.GetUserAsync(principal)` boilerplate across all endpoints
- **ID:** `QUAL-M003`
- **File:** All endpoint files (29 occurrences across 7 files)
- **Risk:** Every single API endpoint starts with `var user = await userManager.GetUserAsync(principal); if (user is null) return Results.Unauthorized();`. This is a database round-trip per request that could be avoided. The user ID is already in the claims principal. While not a bug, the repetition adds unnecessary latency and violates DRY.
- **Evidence:** 29 instances of `var user = await userManager.GetUserAsync(principal)` across CardEndpoints, DeckEndpoints, ReviewEndpoints, etc.
- **Fix:** Extract the user ID directly from the claims principal (`principal.FindFirstValue(ClaimTypes.NameIdentifier)`) in endpoints that only need the ID (most of them). Reserve `GetUserAsync` for endpoints that need the full `AppUser` object (e.g., admin operations). Alternatively, create a shared extension method.

---

### `QUAL-M004` — Pinia stores silently swallow fetch errors, leaving stale state
- **ID:** `QUAL-M004`
- **File:** `fasolt.client/src/stores/cards.ts:L11-21`, `fasolt.client/src/stores/decks.ts:L10-17`, `fasolt.client/src/stores/sources.ts:L10-18`
- **Risk:** The `fetchCards`, `fetchDecks`, and `fetchSources` functions in their respective Pinia stores catch errors only via `finally` (to clear loading state) but don't surface the error to callers. If the API returns a 500 or the network is down, the store silently keeps the old `cards.value` array, and the user sees stale data with no indication of failure.
- **Evidence:**
  ```typescript
  // cards.ts
  async function fetchCards(sourceFile?: string) {
    loading.value = true
    try {
      const response = await apiFetch<PaginatedResponse<Card>>(`/cards?${params}`)
      cards.value = response.items
    } finally {
      loading.value = false   // no error state exposed
    }
  }
  ```
- **Fix:** Add an `error` ref to each store (like `review.ts` already does) and set it in a catch block. Surface this error in the consuming views.

---

### `QUAL-M005` — DevSeedData result of `CreateAsync` not checked for errors
- **ID:** `QUAL-M005`
- **File:** `fasolt.Server/Infrastructure/Data/DevSeedData.cs:L36-37`
- **Risk:** `userManager.CreateAsync(adminUser, DevPassword)` returns an `IdentityResult` that may contain errors (e.g., password policy failures, duplicate detection), but the result is discarded. If user creation fails silently, the subsequent `AddToRoleAsync` will throw a confusing error.
- **Evidence:**
  ```csharp
  await userManager.CreateAsync(adminUser, DevPassword);
  await userManager.AddToRoleAsync(adminUser, "Admin");
  ```
- **Fix:** Check `IdentityResult.Succeeded` and log or throw on failure:
  ```csharp
  var result = await userManager.CreateAsync(adminUser, DevPassword);
  if (!result.Succeeded)
      throw new InvalidOperationException($"Failed to create dev user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
  ```

---

## Low 🔵

### `QUAL-L001` — `AdminView.vue` uses `catch (e: any)` instead of proper error handling
- **ID:** `QUAL-L001`
- **File:** `fasolt.client/src/views/AdminView.vue:L90,L100,L128`
- **Risk:** Three catch blocks use `catch (e: any)` and access `e.message`. If the error is an `ApiError` object (which has `status` and `errors`, not `message`), this will display "undefined" to the admin user.
- **Evidence:**
  ```typescript
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to lock user'
    console.error('Failed to lock user', e)
  }
  ```
- **Fix:** Use the `isApiError` type guard from `@/api/client` and extract error details properly, or use a generic fallback that doesn't rely on `e.message`.

---

### `QUAL-L002` — OAuthEndpoints consent logic duplicated between server-rendered and SPA paths
- **ID:** `QUAL-L002`
- **File:** `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:L393-465` and `L493-554`
- **Risk:** The POST `/oauth/consent` (server-rendered) and POST `/api/oauth/consent` (SPA) handlers contain nearly identical logic for decrypting the authorization query, validating client IDs, and storing consent grants. Changes to one must be mirrored in the other.
- **Evidence:** Both endpoints: decrypt cookie, validate client_id match, store ConsentGrant, redirect. ~40 lines of duplicated logic.
- **Fix:** Extract the shared consent-processing logic into a private helper method.

---

### `QUAL-L003` — `BulkUpdateCards` method makes N+1 database queries
- **ID:** `QUAL-L003`
- **File:** `fasolt.Server/Application/Services/CardService.cs:L265-292`
- **Risk:** `BulkUpdateCards` iterates over items and calls `UpdateCardFields` or `UpdateCardByNaturalKey` individually, each of which queries the database. For a bulk update of 50 cards, this issues 50+ separate queries and 50 separate `SaveChangesAsync` calls. This has poor performance and no transactional atomicity.
- **Evidence:**
  ```csharp
  foreach (var item in items)
  {
      // Each iteration does a DB query + SaveChangesAsync
      if (item.CardId is not null)
          result = await UpdateCardFields(userId, item.CardId, req);
      else if (item.SourceFile is not null && item.Front is not null)
          result = await UpdateCardByNaturalKey(userId, item.SourceFile, item.Front, req);
  }
  ```
- **Fix:** Batch-load all cards in a single query, apply updates in memory, and call `SaveChangesAsync` once. Wrap in a transaction for atomicity.

---

### `QUAL-L004` — `sessionTime` computed property in review store never updates reactively
- **ID:** `QUAL-L004`
- **File:** `fasolt.client/src/stores/review.ts:L35-38`
- **Risk:** `sessionTime` uses `Date.now()` which is not reactive. The computed property will only recalculate when other reactive dependencies change (like `sessionStats.value.startTime`), not when time passes. This means the session timer won't tick.
- **Evidence:**
  ```typescript
  const sessionTime = computed(() => {
    if (!sessionStats.value.startTime) return 0
    return Math.round((Date.now() - sessionStats.value.startTime) / 1000)
  })
  ```
- **Fix:** Use a `setInterval` to update a reactive `now` ref every second, or use `useNow()` from VueUse if available.

---

## Test Coverage Gaps

### Backend (fasolt.Tests/)
- **Covered:** CardService, DeckService, SearchService, OverviewService, SourceService, FSRS scheduling, review flow, notifications
- **Not covered:**
  - **OAuthEndpoints** — the largest and most complex endpoint file (564 lines) has no dedicated tests. The consent flow, dynamic client registration, and token exchange logic are untested.
  - **AdminEndpoints** — no tests for user lock/unlock, admin push trigger, or log retrieval.
  - **AccountEndpoints** — no tests for email change, password change, or account deletion flows.

### Frontend (fasolt.client/src/__tests__/)
- **Covered:** cards store, decks store, review store, sources store, search composable, dark mode, keyboard shortcuts, types
- **Not covered:**
  - **Auth store** — no tests for login, register, logout, or password change flows
  - **AdminView** — no tests for admin UI logic
  - **CardDetailView** — no tests for edit/save/reset flows
  - **API client** (`client.ts`) — no tests for error handling, the `undefined as T` path, or retry logic

---

## What's Done Well

- **Consistent architecture** — Clean separation between endpoints, services, DTOs, and entities. The folder-based Clean Architecture is well-maintained.
- **User data isolation** — Every query consistently filters by `userId`, preventing cross-user data access. This is thorough and consistently applied.
- **SVG sanitization** — Both server-side (whitelist-based XML sanitizer) and client-side (DOMPurify) sanitization is defense-in-depth. The server sanitizer properly strips event handlers, style attributes, and external hrefs.
- **Review store error handling** — The review Pinia store properly exposes an `error` ref and handles failures gracefully, unlike some of the other stores.
- **OAuth security** — PKCE is required, consent is encrypted in cookies, client_id mismatch is validated, and rate limiting is applied to auth endpoints.
- **Notification service resilience** — Invalid device tokens are automatically cleaned up, and the service handles cancellation tokens properly throughout.
- **Cursor-based pagination** — The cards API uses cursor-based pagination correctly, avoiding the performance issues of offset-based pagination at scale.
- **iOS token refresh coalescing** — The `APIClient.swift` properly coalesces concurrent refresh attempts via `refreshTask`, preventing token refresh storms.
