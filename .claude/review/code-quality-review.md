# Code Quality & Architecture Review -- Findings

## Critical 🔴

### `QUAL-C001` -- Duplicate detection in BulkCreate uses case-sensitive HashSet for (SourceFile, Front) tuples
- **ID:** `QUAL-C001`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L238-L240`
- **Risk:** The `existingWithSourceSet` uses a default `HashSet<(string, string)>` which is case-sensitive for both SourceFile and Front. However, the DB query on L226 uses `fronts.Contains(c.Front)` where `fronts` is built with `StringComparer.OrdinalIgnoreCase`. This means the DB returns case-insensitive matches, but the in-memory dedup check at L253 is case-sensitive on the SourceFile component. Cards that differ only in source file casing will bypass duplicate detection, leading to duplicates.
- **Evidence:**
  ```csharp
  var existingWithSourceSet = existingWithSource
      .Select(x => (x.SourceFile!, x.Front))
      .ToHashSet(); // no comparer -- case-sensitive on both components
  ```
- **Fix:** Use a custom `IEqualityComparer<(string, string)>` that does `OrdinalIgnoreCase` on both tuple elements, or normalize casing before inserting into the set.

---

### `QUAL-C002` -- ReviewEndpoints.GetStats "studiedToday" metric is semantically wrong
- **ID:** `QUAL-C002`
- **File:** `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs:L83`
- **Risk:** The `studiedToday` metric counts cards where `Repetitions > 0 && DueAt > todayStart`. This does not actually track cards studied today -- it counts all cards that have ever been reviewed and have a future due date after the start of today. A card studied last week with `DueAt` set to tomorrow would count as "studied today." The metric is fundamentally broken.
- **Evidence:**
  ```csharp
  var studiedToday = await db.Cards.CountAsync(c =>
      c.UserId == user.Id && c.Repetitions > 0 && c.DueAt > todayStart);
  ```
- **Fix:** Track reviews with a separate `ReviewLog` table or add a `LastReviewedAt` field to cards. The current heuristic conflates "has a future due date" with "was studied today."

---

## High 🟠

### `QUAL-H001` -- BearerTokenHandler writes to DB on every authenticated request
- **ID:** `QUAL-H001`
- **File:** `fasolt.Server/Api/Auth/BearerTokenHandler.cs:L52-L53`
- **Risk:** Every API-token-authenticated request triggers a `SaveChangesAsync()` to update `LastUsedAt`. For MCP agents making rapid bulk calls, this adds a write per request, creating unnecessary DB load and contention. Every read-only GET request becomes a write transaction.
- **Evidence:**
  ```csharp
  apiToken.LastUsedAt = DateTimeOffset.UtcNow;
  await db.SaveChangesAsync();
  ```
- **Fix:** Throttle the update (e.g., only update if last-used is older than 5 minutes), or move the update to a background queue / fire-and-forget.

---

### `QUAL-H002` -- SourceEndpoints bypasses UserManager, uses raw claim with null-forgiving operator
- **ID:** `QUAL-H002`
- **File:** `fasolt.Server/Api/Endpoints/SourceEndpoints.cs:L18`
- **Risk:** All other endpoints use `UserManager.GetUserAsync(principal)` to resolve the user and return 401 if null. `SourceEndpoints.List` instead does `user.FindFirstValue(ClaimTypes.NameIdentifier)!` with a null-forgiving operator. If the claim is missing (e.g., misconfigured auth scheme), this will pass a null `userId` to the SQL query rather than returning 401, potentially leaking data or causing a Postgres error.
- **Evidence:**
  ```csharp
  var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
  ```
- **Fix:** Either use `UserManager.GetUserAsync()` like all other endpoints for consistency, or at minimum null-check the claim and return 401.

---

### `QUAL-H003` -- DeckEndpoints.Update returns stale card counts (always 0)
- **ID:** `QUAL-H003`
- **File:** `fasolt.Server/Api/Endpoints/DeckEndpoints.cs:L133`
- **Risk:** After updating a deck, the response always returns `cardCount: 0, dueCount: 0` because the code constructs the DTO with hardcoded zeros. Clients relying on this response will display incorrect counts until they refetch.
- **Evidence:**
  ```csharp
  return Results.Ok(new DeckDto(deck.Id, deck.Name, deck.Description, 0, 0, deck.CreatedAt));
  ```
- **Fix:** Query actual card/due counts before returning, matching the behavior in `DeckEndpoints.List`.

---

### `QUAL-H004` -- Dashboard store uses hardcoded mock data for retention and streak
- **ID:** `QUAL-H004`
- **File:** `fasolt.client/src/stores/dashboard.ts:L6-L11`
- **Risk:** The dashboard store has hardcoded stats (retention "91%", streak "7d") that never change. While `DashboardView.vue` overwrites "Due" and "Total" with real data, retention and streak remain permanently fake. Users see fabricated metrics presented as real data.
- **Evidence:**
  ```typescript
  const stats = ref<Stat[]>([
    { label: 'Due', value: '12', delta: 'up 3 from yesterday' },
    { label: 'Total', value: '84' },
    { label: 'Retention', value: '91%', delta: 'up 3% this week' },
    { label: 'Streak', value: '7d' },
  ])
  ```
- **Fix:** Either implement real retention/streak tracking on the backend, or remove these stats from the UI. Displaying fabricated data is misleading.

---

### `QUAL-H005` -- ChangeEmail skips email verification
- **ID:** `QUAL-H005`
- **File:** `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:L70-L71`
- **Risk:** The email change flow generates a token and immediately uses it in the same request. The user never proves they own the new email address. An attacker with a stolen session could change the account email to one they control.
- **Evidence:**
  ```csharp
  var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
  var result = await userManager.ChangeEmailAsync(user, request.NewEmail, token);
  ```
- **Fix:** Send the token to the new email and require the user to confirm via a separate endpoint, or document this as an intentional tradeoff for simplicity.

---

## Medium 🟡

### `QUAL-M001` -- No global exception handler -- unhandled exceptions produce non-JSON 500 responses
- **ID:** `QUAL-M001`
- **File:** `fasolt.Server/Program.cs:L81`
- **Risk:** `ErrorResponseMiddleware` only handles status codes set by the framework (401, 403, 404) after the response completes. If an endpoint throws an unhandled exception, ASP.NET Core's default handler produces a plain-text or developer-exception-page response, not the structured JSON format the API clients expect. The MCP client's `EnsureSuccess` will fail to parse the error.
- **Evidence:**
  ```csharp
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseMiddleware<ErrorResponseMiddleware>();
  // no app.UseExceptionHandler() or try-catch middleware
  ```
- **Fix:** Add `app.UseExceptionHandler()` or a custom exception-handling middleware early in the pipeline that catches exceptions and returns `{ "error": "internal_error", "message": "..." }`.

---

### `QUAL-M002` -- Review "Again" rating re-queues card, allowing unbounded queue growth
- **ID:** `QUAL-M002`
- **File:** `fasolt.client/src/stores/review.ts:L70`
- **Risk:** When a user rates a card as "Again" (quality 0), the card is pushed back onto the queue. If the user keeps rating cards as "Again," the queue grows without bound and the session can never complete. The progress meter (based on `currentIndex / queue.length`) also regresses, confusing the user.
- **Evidence:**
  ```typescript
  if (quality === 0) {
    sessionStats.value.again++
    queue.value.push({ ...card })
  }
  ```
- **Fix:** Limit re-queues per card (e.g., max 3) or place "Again" cards at a fixed offset rather than the end. Update the progress calculation to account for re-queued cards.

---

### `QUAL-M003` -- CardEndpoints.Create does not initialize SM-2 fields explicitly
- **ID:** `QUAL-M003`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L39-L48`
- **Risk:** The single-card `Create` endpoint relies on C# property defaults for `EaseFactor`, `Interval`, `Repetitions`, and `State`. The `BulkCreate` endpoint (L279-L283) explicitly sets them. The two creation paths could silently diverge if entity defaults change.
- **Evidence:**
  ```csharp
  var card = new Card
  {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      SourceFile = request.SourceFile?.Trim(),
      // ... no EaseFactor, Interval, Repetitions, State
  };
  ```
- **Fix:** Extract a factory method for creating new `Card` instances that both endpoints use, or explicitly set SM-2 fields in `Create`.

---

### `QUAL-M004` -- BulkCreate within-batch dedup uses O(n^2) linear scan
- **ID:** `QUAL-M004`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L263-L265`
- **Risk:** For each card in the batch, the code scans the entire `created` list with `.Any()`. With up to 100 cards, this is 10,000 comparisons worst-case. While not critical at the current 100-card limit, it is an unnecessary quadratic pattern.
- **Evidence:**
  ```csharp
  if (created.Any(c => c.Front.Equals(trimmedFront, StringComparison.OrdinalIgnoreCase) &&
      (c.SourceFile ?? "") == (effectiveSourceFile ?? "")))
  ```
- **Fix:** Use a `HashSet<(string, string)>` with a case-insensitive comparer to track created (front, sourceFile) pairs.

---

### `QUAL-M005` -- Sm2Algorithm applies non-standard 1.3x bonus for "Easy" responses
- **ID:** `QUAL-M005`
- **File:** `fasolt.Server/Application/Services/Sm2Algorithm.cs:L31-L32`
- **Risk:** The standard SM-2 algorithm does not multiply the interval by 1.3 for "Easy." This custom modification causes intervals to grow 30% faster, which deviates from the well-studied SM-2 behavior and may hurt long-term retention.
- **Evidence:**
  ```csharp
  if (quality == 5)
      newInterval = (int)Math.Round(newInterval * 1.3);
  ```
- **Fix:** Document this as an intentional deviation, or remove the bonus to match standard SM-2. Consider making it configurable per user.

---

### `QUAL-M006` -- MCP CardTools.CreateCards does not validate deckId format before Guid.Parse
- **ID:** `QUAL-M006`
- **File:** `fasolt.Mcp/Tools/CardTools.cs:L49`
- **Risk:** If an AI agent passes a malformed `deckId` string, `Guid.Parse(deckId)` throws a `FormatException` that surfaces as an unhandled error to the MCP client.
- **Evidence:**
  ```csharp
  deckId = deckId is not null ? Guid.Parse(deckId) : (Guid?)null,
  ```
- **Fix:** Use `Guid.TryParse` and return a descriptive error string if parsing fails.

---

### `QUAL-M007` -- `apiFetch` always sends Content-Type: application/json, even for GET/DELETE
- **ID:** `QUAL-M007`
- **File:** `fasolt.client/src/api/client.ts:L16-L18`
- **Risk:** The `Content-Type: application/json` header is set unconditionally for all requests including GET and DELETE which have no body. This can trigger unnecessary CORS preflight requests in some configurations.
- **Evidence:**
  ```typescript
  headers: {
    'Content-Type': 'application/json',
    ...options?.headers,
  },
  ```
- **Fix:** Only set `Content-Type` when `options?.body` is defined.

---

## Low 🔵

### `QUAL-L001` -- Repeated user-resolution boilerplate in every endpoint
- **ID:** `QUAL-L001`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L30-L31` (and all other endpoint files)
- **Risk:** Every endpoint method starts with `var user = await userManager.GetUserAsync(principal); if (user is null) return Results.Unauthorized();`. This is duplicated across 15+ methods. A missed null check in a future endpoint would be a security gap.
- **Evidence:**
  ```csharp
  var user = await userManager.GetUserAsync(principal);
  if (user is null) return Results.Unauthorized();
  ```
- **Fix:** Extract into a custom `IEndpointFilter` that resolves the user and short-circuits with 401 if not found.

---

### `QUAL-L002` -- CardEndpoints.ToDto requires DeckCards navigation to be loaded but Create does not load it
- **ID:** `QUAL-L002`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L53`
- **Risk:** `Create` calls `ToDto(card)` after saving, but never loads `DeckCards` or `Deck`. The decks list will always be empty in the create response. Not a crash (empty collection default), but the response is incomplete.
- **Evidence:**
  ```csharp
  return Results.Created($"/api/cards/{card.Id}", ToDto(card));
  ```
- **Fix:** Either load `DeckCards` with `Include` after saving, or return the DTO directly without relying on `ToDto`.

---

### `QUAL-L003` -- `DevEmailSender` registered unconditionally for all environments
- **ID:** `QUAL-L003`
- **File:** `fasolt.Server/Program.cs:L54`
- **Risk:** `DevEmailSender` is registered as the `IEmailSender<AppUser>` for all environments, not just Development. In production, password reset emails would be "sent" via a dev stub that presumably does nothing.
- **Evidence:**
  ```csharp
  builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();
  ```
- **Fix:** Wrap in an environment check or use a configuration-driven factory to register a real email sender for production.

---

### `QUAL-L004` -- Dashboard store `decks` field is dead code
- **ID:** `QUAL-L004`
- **File:** `fasolt.client/src/stores/dashboard.ts:L13-L18`
- **Risk:** The `decks` ref with hardcoded data is never used by any component. `DashboardView.vue` uses `decksStore.decks` instead.
- **Evidence:**
  ```typescript
  const decks = ref<Deck[]>([
    { id: 'deck-1', name: 'Distributed Systems', ... },
    ...
  ])
  ```
- **Fix:** Remove the `decks` field and its `Deck` type import from the dashboard store.

---

### `QUAL-L005` -- DashboardView.vue instantiates `useDashboardStore()` without using it
- **ID:** `QUAL-L005`
- **File:** `fasolt.client/src/views/DashboardView.vue:L13`
- **Risk:** Dead code -- the store is instantiated but never referenced.
- **Evidence:**
  ```typescript
  useDashboardStore()
  ```
- **Fix:** Remove the unused store instantiation and its import.

---

### `QUAL-L006` -- CardsView silently swallows add-to-deck errors
- **ID:** `QUAL-L006`
- **File:** `fasolt.client/src/views/CardsView.vue:L78-L80`
- **Risk:** The `addCardToDeck` function catches all errors with an empty catch block. Users get no feedback when adding a card to a deck fails.
- **Evidence:**
  ```typescript
  } catch {
    // silently fail
  }
  ```
- **Fix:** Show a toast or inline error message on failure.

---

## What's Done Well

- **Clean Architecture boundaries**: The folder-based Clean Architecture (Domain/Application/Infrastructure/Api) is well-structured. Entities have no framework dependencies, DTOs are separate from domain models, and the `AppDbContext` configuration is thorough with proper indexes and constraints.
- **Global query filter for soft deletes**: Using EF Core's `HasQueryFilter(e => e.DeletedAt == null)` on `Card` prevents accidental exposure of deleted cards across all LINQ queries -- a solid, hard-to-forget pattern.
- **Bearer token auth design**: The token hashing approach (SHA-256 of the raw token, prefix storage for display) follows security best practices. Tokens are never stored in plain text. Expiry and revocation are properly checked.
- **Cursor-based pagination**: `CardEndpoints.List` implements proper cursor-based pagination with `CreatedAt + Id` as a stable composite cursor, which is correct for ordered data and avoids the offset-based pagination pitfalls.
- **Bulk creation with atomic validation**: `BulkCreate` validates all cards before creating any (atomic semantics) and handles both in-batch and cross-DB duplicate detection -- a thoughtful design for the MCP agent use case.
- **Frontend store pattern**: Pinia stores are cleanly structured with proper loading states and finally-blocks for cleanup. The review store's session management (queue, progress, rating tracking) is well-designed.
- **MCP server is thin and stateless**: The MCP tools are simple wrappers around API calls, keeping the MCP server as a stateless bridge with no business logic -- exactly the right pattern for maintainability.
- **Consistent endpoint registration**: The use of `MapGroup().RequireAuthorization()` with static extension methods (`MapCardEndpoints()`, etc.) creates a discoverable, uniform API surface.
- **Proper anti-enumeration on forgot-password**: The forgot-password endpoint always returns 200 OK regardless of whether the email exists, preventing email enumeration attacks.
