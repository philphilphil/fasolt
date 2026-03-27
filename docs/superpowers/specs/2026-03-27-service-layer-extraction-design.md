# Service Layer Extraction

**Issue:** #36 — Extract business logic from endpoints into service layer
**Date:** 2026-03-27

## Problem

Four architecture findings from the code review:
- `ReviewEndpoints` contains FSRS scheduling logic, due-card queries, and state mapping (ARCH-H001)
- `NotificationEndpoints` has device token CRUD directly on `AppDbContext` (ARCH-H002)
- `AdminEndpoints` has ~60 lines of push notification assembly and log queries (ARCH-H003)
- `SearchService` imports from `Api.Endpoints` — circular dependency (ARCH-H004)

## Design

### ReviewService

New file: `Application/Services/ReviewService.cs`

Inject: `AppDbContext`, `IScheduler`, `TimeProvider`

Methods:
- `GetDueCards(string userId, int limit, string? deckId)` → `List<DueCardDto>` — due card query with deck active filtering, limit clamping
- `RateCard(string userId, RateCardRequest request)` → `RateCardResponse?` — validate rating, build FSRS card from entity, schedule, map back, save. Returns null if card not found. Throws `ValidationException` for invalid rating.
- `GetStats(string userId)` → `ReviewStatsDto` — due/total/studied-today counts with active deck filtering

Moves into service: `ValidRatings` dictionary, `MapState`, `ParseState` helpers.

`ReviewEndpoints` becomes: resolve user → call service → return result. The `GetOverview` endpoint already delegates to `OverviewService` and stays as-is.

### DeviceTokenService

New file: `Application/Services/DeviceTokenService.cs`

Inject: `AppDbContext`, `UserManager<AppUser>`

Methods:
- `UpsertDeviceToken(string userId, string token)` → void — create or update
- `DeleteDeviceToken(string userId)` → void — idempotent delete
- `GetSettings(string userId)` → `NotificationSettingsResponse` — interval + hasToken
- `UpdateSettings(string userId, int intervalHours)` → `bool` — validate against `AllowedIntervals`, return false if invalid

`AllowedIntervals` array moves into the service.

DTOs (`UpsertDeviceTokenRequest`, `UpdateNotificationSettingsRequest`, `NotificationSettingsResponse`) move to `Application/Dtos/NotificationDtos.cs`.

### AdminService Expansion

Existing file: `Application/Services/AdminService.cs`

Add constructor parameters: `ApnsService`, `TimeProvider`

New methods:
- `GetLogs(int page, int pageSize, string? type)` → paginated log response with type filtering
- `TriggerPushForUser(string userId)` → result object with message and success status. Handles: user lookup, device token check, due card query with deck breakdown, notification assembly, APNs send, token cleanup on 410, logging.

### Search DTOs → Application Layer

Move from `SearchEndpoints.cs` to `Application/Dtos/SearchDtos.cs`:
- `SearchResponse`
- `CardSearchResult`
- `DeckSearchResult`

Update imports in `SearchService.cs` (remove `Api.Endpoints` import) and `SearchEndpoints.cs` (add `Application.Dtos` import).

### Test Changes

**Delete:** `FsrsSchedulingTests.cs` — tests third-party `FSRS.Core` library directly, not app code.

**Rewrite:** `FsrsFullFlowTests.cs` → `ReviewServiceTests.cs`
- Instantiate `ReviewService` with real DB, `IScheduler`, and `FakeTimeProvider`
- Call `ReviewService.RateCard()` and `ReviewService.GetDueCards()` directly
- Remove duplicated `ParseState`/`MapState` helpers and `RateCardInDb` method
- Keep the same test scenarios (lapse/recovery, due card timing, 6-month simulation)
- Add tests for: invalid rating validation, card not found, deck active filtering in `GetDueCards`, `GetStats`

**New:** `DeviceTokenServiceTests.cs`
- Test `UpsertDeviceToken` (create + update), `DeleteDeviceToken` (existing + idempotent), `GetSettings`, `UpdateSettings` (valid + invalid intervals)
- Replaces raw DB tests in `NotificationTests.cs` for token CRUD

**Expand:** admin service tests for `GetLogs` and `TriggerPushForUser`
- Mock `ApnsService` for push tests (success + invalid token paths)

**Keep as-is:** `NotificationTests.cs` eligibility/due-card query tests (they test background service query patterns).

## Non-Goals

- No endpoint behavior changes — all API contracts stay identical
- No new endpoints or DTOs beyond what's needed for the extraction
- No refactoring of code that's already in the service layer
