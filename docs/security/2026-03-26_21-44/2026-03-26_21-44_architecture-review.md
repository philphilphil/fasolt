# Architecture Review — Findings

## Critical 🔴

No findings.

---

## High 🟠

### `ARCH-H001` — ReviewEndpoints bypasses service layer, directly accesses DbContext
- **ID:** `ARCH-H001`
- **File:** `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs:L50-L123`
- **Risk:** The `GetDueCards`, `RateCard`, and `GetStats` endpoints inject `AppDbContext` directly instead of going through a service. This puts FSRS scheduling logic, query construction, and state mapping in the API layer, violating the Application/API boundary. Changes to card review logic require modifying endpoint code rather than a testable service. This also means review logic cannot be reused by MCP tools without duplicating it.
- **Evidence:**
  ```csharp
  // ReviewEndpoints.cs:50 — endpoint directly queries DbContext
  private static async Task<IResult> GetDueCards(
      ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, ...)
  {
      var query = db.Cards.Where(c => c.UserId == user.Id && ...);
      ...
  }

  // ReviewEndpoints.cs:81 — FSRS scheduling logic embedded in endpoint
  private static async Task<IResult> RateCard(
      RateCardRequest request, ..., AppDbContext db, IScheduler scheduler, ...)
  {
      var fsrsCard = card.State == "new" ? new FsrsCard { ... } : new FsrsCard { ... };
      var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);
      card.Stability = updated.Stability;
      ...
  }
  ```
- **Fix:** Extract a `ReviewService` in `Application/Services/` that encapsulates due card queries, FSRS card rating, and stats. The endpoints should delegate to this service just like `CardEndpoints` delegates to `CardService`.

---

### `ARCH-H002` — NotificationEndpoints bypasses service layer, directly accesses DbContext
- **ID:** `ARCH-H002`
- **File:** `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs:L23-L106`
- **Risk:** All notification endpoint handlers (`UpsertDeviceToken`, `DeleteDeviceToken`, `GetSettings`) directly inject and query `AppDbContext`. This means device token CRUD logic lives in the API layer. If another channel (e.g., MCP, background jobs) needs to manage device tokens, the logic must be duplicated.
- **Evidence:**
  ```csharp
  // NotificationEndpoints.cs:26 — endpoint directly manipulates DbContext
  private static async Task<IResult> UpsertDeviceToken(
      ClaimsPrincipal principal, UserManager<AppUser> userManager,
      AppDbContext db, UpsertDeviceTokenRequest request)
  {
      var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == user.Id);
      ...
      db.DeviceTokens.Add(new DeviceToken { ... });
      await db.SaveChangesAsync();
  }
  ```
- **Fix:** Extract a `NotificationService` (or `DeviceTokenService`) in `Application/Services/` and have endpoints delegate to it.

---

### `ARCH-H003` — AdminEndpoints contains business logic that mixes layers
- **ID:** `ARCH-H003`
- **File:** `fasolt.Server/Api/Endpoints/AdminEndpoints.cs:L69-L159`
- **Risk:** `GetLogs` and `TriggerPushForUser` directly use `AppDbContext` with complex query logic (due card aggregation, push sending, log writing) in the endpoint layer. The `TriggerPushForUser` handler is particularly concerning at ~60 lines with notification assembly, APNs call, token validation, and database writes — all in the API layer.
- **Evidence:**
  ```csharp
  // AdminEndpoints.cs:101 — complex business logic in endpoint
  private static async Task<IResult> TriggerPushForUser(
      string id, AppDbContext db, [FromServices] ApnsService apnsService)
  {
      // 60 lines of: due card query, aggregation, notification assembly,
      // APNs call, token cleanup, log writing
  }
  ```
- **Fix:** Move `TriggerPushForUser` logic into `AdminService` or a shared notification service. Move `GetLogs` querying into `AdminService`.

---

### `ARCH-H004` — Application layer references API layer (circular dependency)
- **ID:** `ARCH-H004`
- **File:** `fasolt.Server/Application/Services/SearchService.cs:L2`
- **Risk:** `SearchService` (Application layer) imports `Fasolt.Server.Api.Endpoints` to use `SearchResponse`, `CardSearchResult`, and `DeckSearchResult` DTOs defined in `SearchEndpoints.cs`. This creates a circular dependency between Application and API layers. The Application layer should never depend on the API layer — dependencies should flow inward (API -> Application -> Domain).
- **Evidence:**
  ```csharp
  // SearchService.cs:2
  using Fasolt.Server.Api.Endpoints;

  // SearchEndpoints.cs:30-35 — DTOs defined in API layer
  public record SearchResponse(List<CardSearchResult> Cards, List<DeckSearchResult> Decks);
  public record CardSearchResult(string Id, string Headline, string State);
  public record DeckSearchResult(string Id, string Headline, int CardCount);
  ```
- **Fix:** Move `SearchResponse`, `CardSearchResult`, and `DeckSearchResult` records to `Application/Dtos/` and have both `SearchService` and `SearchEndpoints` reference them from there.

---

## Medium 🟡

### `ARCH-M001` — Application services depend directly on Infrastructure (no abstraction layer)
- **ID:** `ARCH-M001`
- **File:** `fasolt.Server/Application/Services/*.cs`
- **Risk:** All Application services (`CardService`, `DeckService`, `SearchService`, `SourceService`, `OverviewService`, `AdminService`) directly depend on `AppDbContext` from Infrastructure. In Clean Architecture, Application should depend on interfaces defined in Domain, with Infrastructure implementing them. Currently there are no Domain interfaces at all (`Domain/Interfaces/` does not exist). This makes it impossible to unit test services without a real database and tightly couples the Application layer to EF Core/Postgres.
- **Evidence:**
  ```csharp
  // CardService.cs:1,5,9
  using Fasolt.Server.Infrastructure.Data;
  public class CardService(AppDbContext db) { ... }

  // Same pattern in all 6 services
  ```
- **Fix:** For a single-project architecture this is a pragmatic tradeoff, and introducing repository interfaces may be over-engineering at this scale. However, if testability becomes a concern, define `ICardRepository`, `IDeckRepository` etc. in `Domain/Interfaces/` and implement them in `Infrastructure/`. The current approach is acceptable as a conscious decision but should be documented.

---

### `ARCH-M002` — OAuthEndpoints is a monolithic 560-line file mixing HTML rendering, form handling, and API logic
- **ID:** `ARCH-M002`
- **File:** `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`
- **Risk:** This single file handles 8 different endpoints spanning: protected resource metadata, dynamic client registration, authorization, token exchange, HTML login page rendering, login form handling, HTML consent page rendering, consent form handling, plus 2 API fallback endpoints. At 560+ lines with embedded HTML templates, it's the largest endpoint file and the hardest to maintain. The inline HTML makes it difficult to update the OAuth UI without touching endpoint logic.
- **Evidence:** The file contains two full HTML pages (~50 lines each of inline CSS+HTML), OAuth protocol logic, consent grant database operations, and multiple endpoint registrations.
- **Fix:** Consider splitting into: (1) `OAuthProtocolEndpoints` for RFC-level endpoints (authorize, token, register, metadata), (2) `OAuthUiEndpoints` for login/consent HTML rendering, and (3) extracting HTML templates into embedded resources or separate files.

---

### `ARCH-M003` — DTO records scattered across endpoint files instead of centralized
- **ID:** `ARCH-M003`
- **File:** `fasolt.Server/Api/Endpoints/SearchEndpoints.cs:L30-35`, `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs:L109-111`
- **Risk:** Some DTO records are defined at the bottom of endpoint files rather than in `Application/Dtos/`. This causes `ARCH-H004` (SearchService depending on API layer) and makes discoverability harder. The project mostly follows the pattern of DTOs in `Application/Dtos/` but these cases break consistency.
- **Evidence:**
  ```csharp
  // SearchEndpoints.cs:30-35 — DTOs defined in API layer
  public record SearchResponse(...);
  public record CardSearchResult(...);

  // NotificationEndpoints.cs:109-111 — request/response DTOs in API layer
  public record UpsertDeviceTokenRequest(string Token);
  public record UpdateNotificationSettingsRequest(int IntervalHours);
  public record NotificationSettingsResponse(int IntervalHours, bool HasDeviceToken);
  ```
- **Fix:** Move these records to appropriate files in `Application/Dtos/`.

---

### `ARCH-M004` — No domain interfaces or abstractions exist
- **ID:** `ARCH-M004`
- **File:** `fasolt.Server/Domain/`
- **Risk:** The Domain layer contains only entities with no interfaces, value objects, or domain services. The `Domain/Interfaces/` directory mentioned in the project structure documentation does not exist. This means the Domain layer serves purely as a data model rather than encoding business rules. Card state transitions ("new" -> "learning" -> "review") are string-based with no domain validation — any code can set `card.State = "invalid"`.
- **Evidence:** Card state is a plain string with no enum or validation:
  ```csharp
  // Card.cs:23
  public string State { get; set; } = "new";

  // ReviewEndpoints.cs:33-47 — state mapping done in endpoint layer
  private static string MapState(State state) => state switch { ... };
  private static State ParseState(string state) => state switch { ... };
  ```
- **Fix:** Consider a `CardState` enum or value object in Domain. Move state mapping logic from `ReviewEndpoints` into the domain or a service. This is a tradeoff between purity and pragmatism — at current scale, the string approach works but may become a source of bugs as the codebase grows.

---

## Low 🔵

### `ARCH-L001` — Inconsistent service DI registration patterns
- **ID:** `ARCH-L001`
- **File:** `fasolt.Server/Program.cs:L202-220`
- **Risk:** Application services are registered as `Scoped` via individual `AddScoped<>()` calls. Infrastructure services use different patterns: `ApnsService` uses `AddHttpClient<>()`, `NotificationBackgroundService` is `Singleton` (via `AddHostedService`). While functionally correct, there's no service registration extension method (e.g., `AddApplicationServices()`) to group related registrations, making it easy to forget registering a new service.
- **Evidence:**
  ```csharp
  // Program.cs:202-207
  builder.Services.AddScoped<CardService>();
  builder.Services.AddScoped<DeckService>();
  builder.Services.AddScoped<SearchService>();
  builder.Services.AddScoped<AdminService>();
  builder.Services.AddScoped<SourceService>();
  builder.Services.AddScoped<OverviewService>();
  ```
- **Fix:** Consider grouping into extension methods like `services.AddApplicationServices()` and `services.AddInfrastructureServices()` for better organization as the service count grows.

---

### `ARCH-L002` — Frontend API client lacks response type safety for all endpoints
- **ID:** `ARCH-L002`
- **File:** `fasolt.client/src/api/client.ts`
- **Risk:** The API client is a thin wrapper around `fetch` with generic typing via `apiFetch<T>`. Type safety depends entirely on callers passing the correct generic parameter. There is no shared API contract or auto-generated types from the OpenAPI spec.
- **Evidence:**
  ```typescript
  // client.ts:12 — caller must know the correct type
  export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> { ... }

  // stores/sources.ts:13 — type is manually specified
  const data = await apiFetch<{ items: SourceItem[] }>('/sources')
  ```
- **Fix:** Consider generating TypeScript types from the backend's OpenAPI spec to ensure frontend/backend type alignment. This is low-priority since the current approach works but divergence risk grows with the API surface.

---

### `ARCH-L003` — BulkUpdateCards in CardService uses N+1 query pattern
- **ID:** `ARCH-L003`
- **File:** `fasolt.Server/Application/Services/CardService.cs:L265-292`
- **Risk:** `BulkUpdateCards` loops through items and calls `UpdateCardFields` or `UpdateCardByNaturalKey` one by one, each making separate database queries. For large update batches this is inefficient.
- **Evidence:**
  ```csharp
  // CardService.cs:269
  foreach (var item in items)
  {
      // Each iteration: 1 query to find card + 1 query for collision check + 1 SaveChanges
      result = await UpdateCardFields(userId, item.CardId, req);
  }
  ```
- **Fix:** Consider batching reads and writes for bulk operations. Given the MCP use case (typically 1-10 card updates), this is low priority but worth noting for future scale.

---

## What's Done Well

- **Clean endpoint pattern**: The static extension method pattern (`MapCardEndpoints()`, etc.) provides excellent organization and makes the middleware pipeline in `Program.cs` readable at a glance.
- **Consistent user isolation**: Every data-access path includes `userId` filtering. Cards, decks, sources, and device tokens are all scoped to the authenticated user, preventing cross-tenant data leaks.
- **Good API/MCP code reuse**: MCP tools properly delegate to Application services (`CardService`, `DeckService`, `SearchService`) rather than reimplementing logic. The `McpUserResolver` cleanly bridges MCP auth to the user ID needed by services.
- **Well-structured DTOs**: The `Application/Dtos/` folder has clear, purpose-specific DTO records (CardDto, DeckDto, ReviewDtos, etc.) that decouple API responses from domain entities.
- **Frontend architecture is solid**: Stores, views, and API client follow Vue 3 best practices. Pinia stores encapsulate state management, the API client is centralized, and the router has clear auth guards.
- **iOS MVVM pattern holds well**: The iOS app cleanly separates Services (APIClient, AuthService), Repositories (CardRepository, DeckRepository), ViewModels, and Views. The `actor`-based APIClient with coalesced token refresh is well-designed.
- **SVG sanitization**: The `SvgSanitizer` is a thoughtful security measure with allowlists for elements and attributes, blocking event handlers and non-fragment hrefs.
- **Cursor-based pagination**: Card listing uses proper cursor-based pagination rather than offset-based, which scales well.
- **Security headers and rate limiting**: Proper security headers (CSP, X-Frame-Options, HSTS) and differentiated rate limiting policies (auth-strict, auth, api) are configured.
- **Domain entities are lean**: Entities contain only data — no business logic leaks into the data model. Navigation properties are set up correctly for EF Core.
