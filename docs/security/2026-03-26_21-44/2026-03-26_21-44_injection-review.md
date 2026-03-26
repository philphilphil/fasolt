# Injection & Input Validation Review — Findings

**Date:** 2026-03-26
**Scope:** All API endpoints, MCP tools, services, DTOs, SVG sanitization, frontend rendering, search

## Critical 🔴

No findings.

## High 🟠

### `INJ-H001` — Search LIKE pattern injection allows wildcard abuse
- **ID:** `INJ-H001`
- **File:** `fasolt.Server/Application/Services/SearchService.cs:14`
- **Risk:** User-supplied search query is interpolated into a `LIKE` pattern without escaping `%` or `_` wildcards. An attacker can craft queries like `%` or `_%_%_%` to force full-table scans, causing performance degradation. While not a data-breach vector (results are user-scoped), this can be used for targeted DoS against the database.
- **Evidence:**
  ```csharp
  var pattern = $"%{query.Trim()}%";
  // Then used in:
  EF.Functions.ILike(c.Front, pattern)
  ```
  Input like `%` becomes `%%`, matching every row. Input like `_` matches single characters and can be chained for timing-based attacks.
- **Fix:** Escape LIKE metacharacters before interpolation:
  ```csharp
  var escaped = query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
  var pattern = $"%{escaped}%";
  ```
  The `ILike` function in PostgreSQL respects backslash escaping by default.

---

### `INJ-H002` — No length limits on Card front/back text fields
- **ID:** `INJ-H002`
- **File:** `fasolt.Server/Application/Dtos/CardDtos.cs:3` and `fasolt.Server/Application/Dtos/BulkCardDtos.cs:4`
- **Risk:** `Front`, `Back`, `FrontSvg`, and `BackSvg` fields on `CreateCardRequest`, `UpdateCardRequest`, and `BulkCardItem` have no maximum length validation. An attacker can submit arbitrarily large strings (megabytes), consuming database storage and memory. The SVG sanitizer has a 1MB limit, but text fields are unbounded. At 100 cards per bulk request, this allows ~unlimited storage consumption per request.
- **Evidence:**
  ```csharp
  // CardDtos.cs — no length constraints
  public record CreateCardRequest(string? SourceFile, string? SourceHeading, string Front, string Back, ...);

  // Entity config only constrains SourceFile (255 chars), not Front/Back
  entity.Property(e => e.Front).IsRequired(); // no HasMaxLength
  entity.Property(e => e.Back).IsRequired();  // no HasMaxLength
  ```
- **Fix:** Add `[MaxLength]` attributes or explicit validation. Reasonable limits:
  ```csharp
  // In CardEndpoints.cs Create/BulkCreate validation:
  if (request.Front.Length > 10_000 || request.Back.Length > 50_000)
      return Results.BadRequest(...);
  ```
  Also add `HasMaxLength()` in `AppDbContext.OnModelCreating` for defense-in-depth at the database level.

---

## Medium 🟡

### `INJ-M001` — APNs device token used unsanitized in URL path
- **ID:** `INJ-M001`
- **File:** `fasolt.Server/Infrastructure/Services/ApnsService.cs:100`
- **Risk:** The device token string is interpolated directly into the APNs URL path without validation. A malicious device token value could inject path segments or query parameters in the HTTP request to Apple's servers. While exploitation is limited (Apple would reject the request), this is a defense-in-depth gap.
- **Evidence:**
  ```csharp
  var request = new HttpRequestMessage(HttpMethod.Post,
      $"https://{host}/3/device/{deviceToken}");
  ```
- **Fix:** Validate the device token is a hexadecimal string (APNs tokens are 64-character hex):
  ```csharp
  if (!System.Text.RegularExpressions.Regex.IsMatch(deviceToken, @"^[a-fA-F0-9]{64}$"))
      throw new ArgumentException("Invalid APNs device token format");
  ```

---

### `INJ-M002` — No length limit on deck Name and Description at API layer
- **ID:** `INJ-M002`
- **File:** `fasolt.Server/Api/Endpoints/DeckEndpoints.cs:34-41`
- **Risk:** `CreateDeckRequest` and `UpdateDeckRequest` only validate that `Name` is not empty. The DB schema enforces `HasMaxLength(100)` on Name but Description has no limit. An attacker can submit arbitrarily long descriptions. The MCP tool description says "max 100 characters" for name but this is not enforced in code.
- **Evidence:**
  ```csharp
  if (string.IsNullOrWhiteSpace(request.Name))
      return Results.ValidationProblem(...);
  // No length check on Name or Description
  var dto = await deckService.CreateDeck(user.Id, request.Name, request.Description);
  ```
- **Fix:** Add length validation in the endpoint:
  ```csharp
  if (request.Name.Length > 100)
      return Results.ValidationProblem(new Dictionary<string, string[]>
      { ["name"] = ["Name must be 100 characters or fewer."] });
  if (request.Description?.Length > 1000)
      return Results.ValidationProblem(new Dictionary<string, string[]>
      { ["description"] = ["Description must be 1000 characters or fewer."] });
  ```

---

### `INJ-M003` — SVG sanitizer allows `class` and `id` attributes (mXSS risk)
- **ID:** `INJ-M003`
- **File:** `fasolt.Server/Application/Services/SvgSanitizer.cs:23`
- **Risk:** The `class` and `id` attributes are in the SVG allowlist. If any CSS in the host page applies styles based on class or id selectors, a crafted SVG could hijack existing classes (e.g., `.hidden { display: none }` to hide page elements, or apply existing animation/overlay styles). This is a limited CSS-injection vector. While not script execution, it could be used for UI redressing.
- **Evidence:**
  ```csharp
  private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
  {
      // ...
      "class", "id",
      // ...
  };
  ```
- **Fix:** Remove `class` from the allowlist (less risk). For `id`, scope by prefixing to avoid collision with host page IDs, or remove if internal `<use href="#id">` references are not needed in practice.

---

### `INJ-M004` — Open redirect via OAuth consent `redirectUrl`
- **ID:** `INJ-M004`
- **File:** `fasolt.client/src/views/OAuthConsentView.vue:58`
- **Risk:** The Vue consent view sets `window.location.href = data.redirectUrl` from a server API response. The server returns the client's registered redirect URI on denial. While the redirect URIs are validated at client registration time (HTTPS required, or pattern-matched), a compromised or malicious-but-valid redirect URI could redirect the user to a phishing site after denial. The risk is mitigated by the fact that only registered redirect URIs are used.
- **Evidence:**
  ```typescript
  // Vue client blindly follows the server-provided URL
  const data = await res.json()
  window.location.href = data.redirectUrl
  ```
  ```csharp
  // Server returns the client's redirect URI
  var clientRedirectUri = redirectUris.FirstOrDefault() ?? "/";
  return Results.Ok(new { redirectUrl = $"{clientRedirectUri}{separator}error=access_denied" });
  ```
- **Fix:** This is acceptable given the OAuth spec requires redirect to the client's registered URI. However, consider validating the URL scheme in the frontend as defense-in-depth:
  ```typescript
  if (data.redirectUrl.startsWith('/') || data.redirectUrl.startsWith('https://'))
      window.location.href = data.redirectUrl
  ```

---

### `INJ-M005` — No validation on `SourceHeading` length
- **ID:** `INJ-M005`
- **File:** `fasolt.Server/Domain/Entities/Card.cs:12` and `fasolt.Server/Infrastructure/Data/AppDbContext.cs:26-46`
- **Risk:** `SourceHeading` has no `HasMaxLength()` constraint in the database configuration or DTO validation. While less impactful than front/back text, it should still be bounded.
- **Evidence:**
  ```csharp
  // AppDbContext.cs — SourceFile has max length, SourceHeading does not
  entity.Property(e => e.SourceFile).HasMaxLength(255);
  // No constraint on SourceHeading
  ```
- **Fix:** Add `entity.Property(e => e.SourceHeading).HasMaxLength(500);` and validate at the API layer.

---

## Low 🔵

### `INJ-L001` — Bulk update endpoint accepts unbounded item count
- **ID:** `INJ-L001`
- **File:** `fasolt.Server/Api/McpTools/CardTools.cs:81-94`
- **Risk:** The `UpdateCards` MCP tool and the underlying `BulkUpdateCards` method accept an unbounded list of update items. Unlike `BulkCreate` (capped at 100), updates have no limit, allowing a client to submit thousands of updates in a single request, each triggering individual DB queries.
- **Evidence:**
  ```csharp
  // CardTools.cs — only checks for empty, no upper bound
  if (cards.Count == 0)
      return JsonSerializer.Serialize(new { error = "..." }, McpJson.Options);
  var results = await cardService.BulkUpdateCards(userId, cards);
  ```
- **Fix:** Add an upper bound:
  ```csharp
  if (cards.Count > 100)
      return JsonSerializer.Serialize(new { error = "Maximum 100 cards per update request" }, McpJson.Options);
  ```

---

### `INJ-L002` — MCP CreateDeck tool doesn't validate name length
- **ID:** `INJ-L002`
- **File:** `fasolt.Server/Api/McpTools/DeckTools.cs:20-27`
- **Risk:** The `CreateDeck` MCP tool description says "max 100 characters" but no code enforces this. The DB constraint (`HasMaxLength(100)`) will throw an exception if exceeded, resulting in a 500 error instead of a clean validation error.
- **Evidence:**
  ```csharp
  [McpServerTool, Description("Create a new deck for organizing flashcards.")]
  public async Task<string> CreateDeck(
      [Description("Deck name (max 100 characters)")] string name,
      ...
  ```
- **Fix:** Add validation before calling the service:
  ```csharp
  if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
      return JsonSerializer.Serialize(new { error = "Name is required and must be 100 characters or fewer" }, McpJson.Options);
  ```

---

### `INJ-L003` — MCP tools and API endpoints don't validate empty/whitespace-only SourceHeading
- **ID:** `INJ-L003`
- **File:** `fasolt.Server/Application/Services/CardService.cs:19`
- **Risk:** `SourceHeading` is stored as-is (no trimming on `CreateCard`, only trimmed in `ApplyCardFieldUpdates`). Inconsistent trimming across code paths can lead to subtle data quality issues and make deduplication checks unreliable.
- **Evidence:**
  ```csharp
  // CreateCard: sourceHeading not trimmed
  SourceHeading = sourceHeading,

  // ApplyCardFieldUpdates: sourceHeading IS trimmed
  if (req.NewSourceHeading is not null) card.SourceHeading = req.NewSourceHeading.Trim();
  ```
- **Fix:** Apply consistent trimming in `CreateCard`:
  ```csharp
  SourceHeading = sourceHeading?.Trim(),
  ```

---

### `INJ-L004` — DeleteCards MCP tool allows both cardIds and sourceFile simultaneously
- **ID:** `INJ-L004`
- **File:** `fasolt.Server/Api/McpTools/CardTools.cs:60-77`
- **Risk:** The `DeleteCards` tool processes both `cardIds` and `sourceFile` in the same request with additive delete counts. A confused caller could inadvertently delete more cards than intended. This is a usability/safety issue rather than a security vulnerability.
- **Evidence:**
  ```csharp
  if (cardIds is not null && cardIds.Count > 0)
      count += await cardService.DeleteCards(userId, cardIds);
  if (sourceFile is not null)
      count += await cardService.DeleteCardsBySource(userId, sourceFile);
  ```
- **Fix:** Consider making these mutually exclusive, or clearly documenting additive behavior.

---

## What's Done Well

- **No raw SQL injection vectors.** All EF Core queries use LINQ or parameterized `SqlQueryRaw` (SourceService uses `{0}` / `{1}` positional parameters, which EF Core correctly parameterizes). No `FromSqlRaw` with string interpolation.
- **Solid XSS defense-in-depth.** All `v-html` bindings in the frontend pass through either `DOMPurify.sanitize()` (via `useMarkdown.render()`) or `sanitizeSvg()`. Markdown-it is configured with `html: false`, preventing raw HTML pass-through.
- **Server-side SVG sanitizer is well-designed.** Allowlisted elements and attributes, event handler stripping (`on*`), `style` attribute removal, and external `href` blocking create strong defense against SVG-based XSS.
- **Frontend SVG sanitizer provides defense-in-depth.** Uses DOMPurify with `svg` profile, forbids `foreignObject`, `script`, and `style` tags, and disables data attributes.
- **No command injection risk.** No `Process.Start`, shell execution, or OS command construction anywhere in the codebase.
- **No path traversal risk.** Source file names are metadata strings stored in the DB, never used to access the filesystem.
- **OAuth HTML pages properly encode output.** `HtmlEncode` and `HtmlAttributeEncode` are correctly used for user-controlled values in server-rendered OAuth login/consent pages.
- **Open redirect protection on OAuth login.** The `IsLocalUrl` check prevents external redirect via the `returnUrl` parameter.
- **Rate limiting applied consistently.** All endpoint groups have `.RequireRateLimiting()` applied.
- **User scoping on all queries.** Every data-access query includes `UserId` filtering, preventing cross-user data access.
