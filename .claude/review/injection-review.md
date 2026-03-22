# Injection & Input Validation Review — Findings

## Critical 🔴

No findings.

---

## High 🟠

### `INJ-H001` — Stored XSS via Search Headline Rendered with v-html
- **ID:** `INJ-H001`
- **File:** `fasolt.client/src/components/SearchResults.vue:L57,L79`
- **Risk:** The server-side `ts_headline` function wraps matching terms in `<mark>` tags, but any HTML already present in the card `Front`/`Back` or deck `Name` fields will be passed through unescaped. Since these headline strings are rendered with `v-html`, a user who creates a card with a `Front` like `<img src=x onerror=alert(1)>` will have the payload execute when anyone searches for content matching that card. Although cards are per-user (self-XSS in the current model), this becomes exploitable if shared decks, admin views, or multi-tenant search are ever added.
- **Evidence:**
  ```html
  <!-- SearchResults.vue:57 -->
  <span class="truncate" v-html="card.headline" />
  <!-- SearchResults.vue:79 -->
  <span class="truncate" v-html="deck.headline" />
  ```
  The `headline` comes from Postgres `ts_headline` which only wraps matched tokens in `<mark>` but does not sanitize other HTML in the source text. The server query in `SearchEndpoints.cs:L32-L43` returns this raw.
- **Fix:** Sanitize the `headline` on the client before rendering. Either (a) HTML-entity-encode the headline and then selectively allow only `<mark>` tags, or (b) use a lightweight sanitizer like DOMPurify configured to allow only `<mark>`. Alternatively, strip HTML server-side before passing text to `ts_headline`, or return plain-text match positions and highlight client-side.

---

### `INJ-H002` — Stored XSS via Markdown-Rendered Card Content
- **ID:** `INJ-H002`
- **File:** `fasolt.client/src/composables/useMarkdown.ts:L3-L4` and `fasolt.client/src/components/ReviewCard.vue:L23,L26`
- **Risk:** Card `front` and `back` text is rendered through `markdown-it` with `html: false`, which does prevent raw HTML passthrough. However, `markdown-it` with default settings can still produce link-based XSS via `[click](javascript:alert(1))` depending on version and configuration. The `linkify: true` option also auto-links URLs which may include `javascript:` scheme URIs. While `markdown-it` does block `javascript:` links by default in recent versions, any future version regression or configuration change would expose XSS. The rendered output is injected via `v-html` in multiple components.
- **Evidence:**
  ```typescript
  const md = new MarkdownIt({
    html: false,
    linkify: true,
    typographer: false,
  })
  ```
  ```html
  <div class="prose prose-sm dark:prose-invert max-w-none" v-html="render(card.front)" />
  <div class="prose prose-sm dark:prose-invert max-w-none" v-html="render(card.back)" />
  ```
  Used in: `ReviewCard.vue:L23,L26`, `CardDetailView.vue:L138,L142`, `CardEditDialog.vue:L74,L85`, `CardCreateDialog.vue:L120,L133,L154`.
- **Fix:** Add DOMPurify as a post-processing step after `md.render()` to sanitize the output HTML. Example: `return DOMPurify.sanitize(md.render(content))`. This provides defense-in-depth regardless of markdown-it's internal link sanitization.

---

## Medium 🟡

### `INJ-M001` — No Max-Length Validation on Card Front/Back Fields
- **ID:** `INJ-M001`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L33-L37` and `fasolt.Server/Infrastructure/Data/AppDbContext.cs:L25-26`
- **Risk:** The `Front` and `Back` properties on `Card` have no `HasMaxLength` constraint in EF Core configuration, and no server-side length validation in the `Create`, `BulkCreate`, or `Update` endpoints. An attacker can submit arbitrarily large strings (multi-MB) causing excessive database storage, slow queries on the computed `tsvector` search column, and potential denial of service.
- **Evidence:**
  ```csharp
  // AppDbContext.cs - no max length for Front/Back
  entity.Property(e => e.Front).IsRequired();
  entity.Property(e => e.Back).IsRequired();

  // CardEndpoints.cs - only checks IsNullOrWhiteSpace, no length check
  if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
  ```
- **Fix:** Add `HasMaxLength(10000)` (or a suitable limit) to both `Front` and `Back` in `AppDbContext.OnModelCreating`, create a migration, and add server-side length validation in the `Create`, `Update`, and `BulkCreate` endpoints.

---

### `INJ-M002` — No Max-Length Validation on DisplayName
- **ID:** `INJ-M002`
- **File:** `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:L45` and `fasolt.Server/Domain/Entities/AppUser.cs:L7`
- **Risk:** The `UpdateProfile` endpoint sets `user.DisplayName = request.DisplayName` with no length validation. The `DisplayName` property has no `HasMaxLength` constraint. Attackers can submit extremely long display names causing storage and rendering issues.
- **Evidence:**
  ```csharp
  // AccountEndpoints.cs:45
  user.DisplayName = request.DisplayName;

  // AppUser.cs:7
  public string? DisplayName { get; set; }
  ```
- **Fix:** Add length validation (e.g., max 100 characters) in the `UpdateProfile` endpoint and add a `HasMaxLength` constraint on the entity configuration.

---

### `INJ-M003` — Unbounded `limit` Parameter in ReviewEndpoints.GetDueCards
- **ID:** `INJ-M003`
- **File:** `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs:L24`
- **Risk:** The `GetDueCards` endpoint accepts a `limit` query parameter with no upper bound clamping. An attacker can request `limit=999999` to force the server to load an extremely large result set into memory, causing potential OOM or slow response.
- **Evidence:**
  ```csharp
  private static async Task<IResult> GetDueCards(
      ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, int limit = 50, Guid? deckId = null)
  {
      // ...
      .Take(limit)  // No clamping — unlike CardEndpoints which uses Math.Clamp(limit ?? 50, 1, 200)
  ```
- **Fix:** Add clamping similar to `CardEndpoints.List`: `var take = Math.Clamp(limit, 1, 200);` and use `take` in the `.Take()` call.

---

### `INJ-M004` — No Length Validation on SourceFile and SourceHeading Fields
- **ID:** `INJ-M004`
- **File:** `fasolt.Server/Api/Endpoints/CardEndpoints.cs:L43-44`
- **Risk:** While `SourceFile` has a `HasMaxLength(255)` at the database level (which will throw an exception on overflow), `SourceHeading` has no max length at all. Additionally, the 255-char database constraint on `SourceFile` is not validated at the API level, meaning the error manifests as an unhandled DB exception rather than a clean validation error.
- **Evidence:**
  ```csharp
  // CardEndpoints.cs:43-44
  SourceFile = request.SourceFile?.Trim(),
  SourceHeading = request.SourceHeading,  // No length check, no max length in DB

  // AppDbContext.cs:27 — SourceFile has DB constraint but no API validation
  entity.Property(e => e.SourceFile).HasMaxLength(255);
  // No HasMaxLength for SourceHeading
  ```
- **Fix:** Add API-level length validation for both `SourceFile` (max 255) and `SourceHeading` (e.g., max 500) in the `Create` and `BulkCreate` endpoints. Add `HasMaxLength` for `SourceHeading` in the entity configuration.

---

## Low 🔵

### `INJ-L001` — MCP Tool Parses deckId with Guid.Parse Without Error Handling
- **ID:** `INJ-L001`
- **File:** `fasolt.Mcp/Tools/CardTools.cs:L49`
- **Risk:** If an AI agent passes a malformed `deckId` string, `Guid.Parse` throws a `FormatException` that surfaces as an unhandled MCP tool error rather than a user-friendly message.
- **Evidence:**
  ```csharp
  deckId = deckId is not null ? Guid.Parse(deckId) : (Guid?)null,
  ```
- **Fix:** Use `Guid.TryParse` and return a descriptive error message if parsing fails.

---

### `INJ-L002` — Description Field on Deck Has No Max-Length Constraint
- **ID:** `INJ-L002`
- **File:** `fasolt.Server/Infrastructure/Data/AppDbContext.cs:L43-48`
- **Risk:** The `Deck.Description` field has no `HasMaxLength` constraint at the database or API level. While lower risk than card content (decks are fewer in number), arbitrarily large descriptions are still possible.
- **Evidence:**
  ```csharp
  // Only Name has a constraint
  entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
  // Description has no constraint
  ```
- **Fix:** Add `HasMaxLength(1000)` (or suitable limit) for `Description` and validate at the API level.

---

## What's Done Well

- **No SQL injection:** All database queries use either EF Core LINQ (parameterized by default) or `SqlQueryRaw` with positional `{0}`, `{1}` parameters, which EF Core correctly parameterizes. The `plainto_tsquery` function in `SearchEndpoints.cs` and `SourceEndpoints.cs` is used correctly with parameterized inputs -- `plainto_tsquery` also inherently prevents tsquery injection by treating input as plain text rather than query syntax.
- **Authorization and tenant isolation:** Every endpoint checks `UserId` on queries, preventing cross-user data access. Route parameters use `{id:guid}` type constraints.
- **No command injection or SSRF:** No process execution or server-side URL fetching based on user input. The MCP server uses a fixed `FASOLT_URL` base and only appends safe, URI-encoded path segments.
- **No path traversal:** The `SourceFile` field is stored as metadata only and never used to access the filesystem on the server.
- **Soft deletes with global query filter:** `Card` entities use `HasQueryFilter(e => e.DeletedAt == null)`, preventing deleted cards from appearing in normal queries.
- **markdown-it configured with `html: false`:** Raw HTML in card content is disabled at the markdown parser level, which blocks the most common XSS vector through markdown rendering.
- **Pagination clamping on card list:** `CardEndpoints.List` properly clamps the `limit` parameter with `Math.Clamp(limit ?? 50, 1, 200)`.
- **Bulk create has a count limit:** The `BulkCreate` endpoint limits requests to 100 cards, preventing bulk abuse.
- **API token hashing:** Tokens are stored as SHA-256 hashes, not in plaintext.
- **Error middleware does not leak internals:** The `ErrorResponseMiddleware` returns generic error messages for 401/403/404 without exposing stack traces or internal details.
- **Image rendering sanitized in markdown:** The custom `image` renderer in `useMarkdown.ts` properly calls `md.utils.escapeHtml(raw)` on the alt text before embedding it in HTML.
