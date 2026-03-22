# Security & Code Review — Overview

**Repo:** fasolt | **Date:** 2026-03-22 | **Files Reviewed:** ~75 source files across backend (.NET), frontend (Vue/TS), MCP server, and infrastructure configs

## Risk Summary

| Severity | Count | Top Areas |
|----------|-------|-----------|
| 🔴 Critical | 5 | Auth (3), Code Quality (2) |
| 🟠 High | 21 | Auth (4), Code Quality (5), Infra (4), Data (3), Dep (3), Injection (2) |
| 🟡 Medium | 28 | Code Quality (7), Infra (5), Injection (4), Auth (4), Data (4), Dep (4) |
| 🔵 Low | 21 | Code Quality (6), Data (4), Auth (3), Dep (3), Infra (3), Injection (2) |

> **Note:** Several findings overlap across agents (e.g., DevEmailSender flagged by AUTH, DATA, INFRA, and QUAL; DB credentials flagged by AUTH, DATA, and INFRA). De-duplicated unique issues are ~45.

## Top 5 Priority Fixes

1. `AUTH-C002` **Email Change Bypasses Verification** — generates and immediately consumes the change-email token in one request, enabling account takeover via session hijack → see [auth-review.md](auth-review.md)

2. `AUTH-C003` **No CSRF Protection** — no antiforgery tokens on any state-changing endpoint; combined with AUTH-C002, allows full account takeover if any same-site XSS vector exists → see [auth-review.md](auth-review.md)

3. `INJ-H001` / `INJ-H002` **Stored XSS via v-html** — search headlines from `ts_headline` and markdown-rendered card content both use `v-html` without DOMPurify sanitization → see [injection-review.md](injection-review.md)

4. `AUTH-H001` / `INFRA-H004` **DevEmailSender Registered Unconditionally** — password reset tokens logged instead of emailed in production; single-line fix (`if (builder.Environment.IsDevelopment())`) resolves this → see [auth-review.md](auth-review.md), [infra-review.md](infra-review.md)

5. `QUAL-C001` **Case-Sensitive Duplicate Detection in BulkCreate** — `HashSet` uses default case-sensitive comparison while DB query uses case-insensitive, allowing duplicates to bypass detection → see [code-quality-review.md](code-quality-review.md)

## Per-Area Summaries

### Dependencies — [🔴 0 / 🟠 3 / 🟡 4 / 🔵 3]

The MCP project uses wildcard NuGet version ranges (`0.*`, `10.*`) which create non-deterministic builds and supply chain risk. No NuGet lock files are committed anywhere. The Docker Postgres image uses a floating tag without digest pinning. The npm side is well-managed with a committed lockfile and private flag. The server project properly pins all NuGet versions.

### Auth & Authorization — [🔴 3 / 🟠 4 / 🟡 4 / 🔵 3]

The most critical area. The email change endpoint bypasses verification by generating and immediately consuming the token. No CSRF protection exists (mitigated partially by SameSite=Strict cookies). DB credentials are in the base appsettings.json. DevEmailSender is registered unconditionally, logging security tokens in all environments. No rate limiting on auth endpoints. On the positive side: token hashing is excellent, user isolation is consistent across all endpoints, and cookie flags (HttpOnly, SameSite=Strict) are properly set.

### Injection & Input Validation — [🔴 0 / 🟠 2 / 🟡 4 / 🔵 2]

No SQL injection, command injection, SSRF, or path traversal vulnerabilities. The two high findings are both XSS vectors: search result headlines rendered via `v-html` without sanitization, and markdown-rendered card content using `v-html` without DOMPurify. Multiple fields lack max-length validation (card Front/Back, DisplayName, SourceHeading), and the review endpoint's `limit` parameter has no upper bound. EF Core parameterized queries are used correctly throughout.

### Data Exposure & Cryptography — [🔴 0 / 🟠 3 / 🟡 4 / 🔵 4]

DB credentials in version control and DevEmailSender logging security tokens are the top concerns (overlapping with AUTH/INFRA). Cookie SecurePolicy is `SameAsRequest` instead of `Always` for production. Email enumeration is possible via the ChangeEmail endpoint. No HTTPS redirection or HSTS configured. Positives: API tokens use SHA-256 hashing, no sensitive data in frontend localStorage, and error responses don't leak internals.

### Code Quality & Architecture — [🔴 2 / 🟠 5 / 🟡 7 / 🔵 6]

Two critical bugs: case-sensitive duplicate detection in BulkCreate, and a semantically broken "studiedToday" metric that counts all previously-reviewed cards rather than cards studied today. The dashboard displays hardcoded fake stats for retention and streak. DeckEndpoints.Update returns hardcoded zero counts. The bearer token handler writes to the DB on every authenticated request. Architecture is clean overall — proper Clean Architecture boundaries, cursor-based pagination, atomic bulk validation, and a thin stateless MCP server.

### Configuration & Infrastructure — [🔴 0 / 🟠 4 / 🟡 5 / 🔵 3]

No CORS policy, no security headers (CSP, X-Frame-Options, HSTS), and no CI/CD pipeline. Postgres port is exposed on all interfaces in docker-compose. No Docker health check for the database container. AllowedHosts is set to wildcard. Dev seed data gating and error response handling are done well.

## Positive Patterns Observed

- **API token security**: SHA-256 hashing, one-time display, expiration + revocation support, prefix for identification
- **User data isolation**: Every data endpoint consistently filters by `UserId` — no IDOR vulnerabilities found
- **Soft delete with global query filter**: EF Core `HasQueryFilter` prevents deleted cards from leaking
- **Parameterized SQL**: All raw SQL uses EF Core parameterized queries correctly, including `plainto_tsquery`
- **Clean Architecture**: Well-structured domain/application/infrastructure/API boundaries
- **Cursor-based pagination**: Proper composite cursor (CreatedAt + Id) avoids offset pagination pitfalls
- **Atomic bulk validation**: BulkCreate validates all cards before creating any
- **Thin MCP server**: Stateless bridge pattern with no business logic
- **Anti-enumeration on forgot-password**: Always returns 200 OK regardless of email existence
- **Cookie hardening**: HttpOnly=true, SameSite=Strict properly configured
- **Frontend**: No tokens in localStorage, proper credentials handling, private npm package

## Full Finding Index

| ID | Severity | Title | File | Report |
|----|----------|-------|------|--------|
| `AUTH-C001` | 🔴 | DB Credentials in appsettings.json | `appsettings.json:L3` | [auth-review.md](auth-review.md) |
| `AUTH-C002` | 🔴 | Email Change Bypasses Verification | `AccountEndpoints.cs:L70-71` | [auth-review.md](auth-review.md) |
| `AUTH-C003` | 🔴 | No CSRF Protection | `Program.cs` | [auth-review.md](auth-review.md) |
| `QUAL-C001` | 🔴 | Case-Sensitive Duplicate Detection in BulkCreate | `CardEndpoints.cs:L238-240` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-C002` | 🔴 | Broken "studiedToday" Metric | `ReviewEndpoints.cs:L83` | [code-quality-review.md](code-quality-review.md) |
| `AUTH-H001` | 🟠 | DevEmailSender Used Unconditionally | `Program.cs:L54` | [auth-review.md](auth-review.md) |
| `AUTH-H002` | 🟠 | No Rate Limiting on Auth Endpoints | `Program.cs` | [auth-review.md](auth-review.md) |
| `AUTH-H003` | 🟠 | Cookie SecurePolicy SameAsRequest | `Program.cs:L39` | [auth-review.md](auth-review.md) |
| `AUTH-H004` | 🟠 | Health Endpoint Not AllowAnonymous | `HealthEndpoints.cs:L7` | [auth-review.md](auth-review.md) |
| `DATA-H001` | 🟠 | DB Credentials in Version Control | `appsettings.json:L3` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-H002` | 🟠 | DevEmailSender Logs Reset Tokens | `DevEmailSender.cs:L23,29` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-H003` | 🟠 | DevEmailSender Not Dev-Only | `Program.cs:L54` | [data-exposure-review.md](data-exposure-review.md) |
| `DEP-H001` | 🟠 | Wildcard NuGet Versions in MCP | `fasolt.Mcp.csproj:L14-16` | [dependency-audit.md](dependency-audit.md) |
| `DEP-H002` | 🟠 | Docker Image Tag Not Pinned | `docker-compose.yml:L3` | [dependency-audit.md](dependency-audit.md) |
| `DEP-H003` | 🟠 | No NuGet Lock Files | Project-wide | [dependency-audit.md](dependency-audit.md) |
| `INFRA-H001` | 🟠 | DB Credentials in appsettings.json | `appsettings.json:L3` | [infra-review.md](infra-review.md) |
| `INFRA-H002` | 🟠 | No CORS Policy | `Program.cs` | [infra-review.md](infra-review.md) |
| `INFRA-H003` | 🟠 | No Security Headers | `Program.cs` | [infra-review.md](infra-review.md) |
| `INFRA-H004` | 🟠 | DevEmailSender Unconditional | `Program.cs:L54` | [infra-review.md](infra-review.md) |
| `INJ-H001` | 🟠 | Stored XSS via Search Headline v-html | `SearchResults.vue:L57,79` | [injection-review.md](injection-review.md) |
| `INJ-H002` | 🟠 | Stored XSS via Markdown v-html | `useMarkdown.ts:L3-4` | [injection-review.md](injection-review.md) |
| `QUAL-H001` | 🟠 | DB Write on Every Auth Request | `BearerTokenHandler.cs:L52-53` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-H002` | 🟠 | SourceEndpoints Null-Safety Bypass | `SourceEndpoints.cs:L18` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-H003` | 🟠 | DeckEndpoints Returns Stale Counts | `DeckEndpoints.cs:L133` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-H004` | 🟠 | Hardcoded Fake Dashboard Stats | `dashboard.ts:L6-11` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-H005` | 🟠 | ChangeEmail Skips Verification | `AccountEndpoints.cs:L70-71` | [code-quality-review.md](code-quality-review.md) |
| `AUTH-M001` | 🟡 | Token LastUsedAt Write Amplification | `BearerTokenHandler.cs:L52-53` | [auth-review.md](auth-review.md) |
| `AUTH-M002` | 🟡 | Dev Seed Data Single Guard | `Program.cs:L74-77` | [auth-review.md](auth-review.md) |
| `AUTH-M003` | 🟡 | Identity API Endpoints Fully Exposed | `Program.cs:L91` | [auth-review.md](auth-review.md) |
| `AUTH-M004` | 🟡 | 1-Day Sliding Session, No Absolute Expiry | `Program.cs:L40-41` | [auth-review.md](auth-review.md) |
| `DATA-M001` | 🟡 | Cookie SecurePolicy SameAsRequest | `Program.cs:L39` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-M002` | 🟡 | Email Enumeration via ChangeEmail | `AccountEndpoints.cs:L64-69` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-M003` | 🟡 | No HTTPS Redirection or HSTS | `Program.cs` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-M004` | 🟡 | Token Lookup Timing Attack Surface | `BearerTokenHandler.cs:L36-43` | [data-exposure-review.md](data-exposure-review.md) |
| `DEP-M001` | 🟡 | ModelContextProtocol Pre-release 0.x | `fasolt.Mcp.csproj:L14` | [dependency-audit.md](dependency-audit.md) |
| `DEP-M002` | 🟡 | npm Caret Ranges | `package.json:L14-41` | [dependency-audit.md](dependency-audit.md) |
| `DEP-M003` | 🟡 | No .npmrc Restricting Registry | `fasolt.client/` | [dependency-audit.md](dependency-audit.md) |
| `DEP-M004` | 🟡 | EF Core Design in Production Build | `fasolt.Server.csproj:L13-16` | [dependency-audit.md](dependency-audit.md) |
| `INFRA-M001` | 🟡 | Cookie SecurePolicy SameAsRequest | `Program.cs:L39` | [infra-review.md](infra-review.md) |
| `INFRA-M002` | 🟡 | No Rate Limiting | `Program.cs` | [infra-review.md](infra-review.md) |
| `INFRA-M003` | 🟡 | Postgres Port Exposed on All Interfaces | `docker-compose.yml:L10` | [infra-review.md](infra-review.md) |
| `INFRA-M004` | 🟡 | No Docker Health Check | `docker-compose.yml` | [infra-review.md](infra-review.md) |
| `INFRA-M005` | 🟡 | Wildcard AllowedHosts | `appsettings.json:L11` | [infra-review.md](infra-review.md) |
| `INJ-M001` | 🟡 | No Max-Length on Card Front/Back | `CardEndpoints.cs:L33-37` | [injection-review.md](injection-review.md) |
| `INJ-M002` | 🟡 | No Max-Length on DisplayName | `AccountEndpoints.cs:L45` | [injection-review.md](injection-review.md) |
| `INJ-M003` | 🟡 | Unbounded limit in GetDueCards | `ReviewEndpoints.cs:L24` | [injection-review.md](injection-review.md) |
| `INJ-M004` | 🟡 | No Length Validation on SourceHeading | `CardEndpoints.cs:L43-44` | [injection-review.md](injection-review.md) |
| `QUAL-M001` | 🟡 | No Global Exception Handler | `Program.cs:L81` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-M002` | 🟡 | Unbounded "Again" Re-queue | `review.ts:L70` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-M003` | 🟡 | Inconsistent SM-2 Field Init | `CardEndpoints.cs:L39-48` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-M004` | 🟡 | O(n²) Batch Dedup | `CardEndpoints.cs:L263-265` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-M005` | 🟡 | Non-Standard SM-2 Easy Bonus | `Sm2Algorithm.cs:L31-32` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-M006` | 🟡 | MCP Guid.Parse Without Validation | `CardTools.cs:L49` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-M007` | 🟡 | Unnecessary Content-Type on GET | `client.ts:L16-18` | [code-quality-review.md](code-quality-review.md) |
| `AUTH-L001` | 🔵 | No Special Char in Password Policy | `Program.cs:L22` | [auth-review.md](auth-review.md) |
| `AUTH-L002` | 🔵 | Short Lockout Duration | `Program.cs:L27` | [auth-review.md](auth-review.md) |
| `AUTH-L003` | 🔵 | Token Prefix Info Leak | `ApiTokenEndpoints.cs:L42` | [auth-review.md](auth-review.md) |
| `DATA-L001` | 🔵 | No Special Char in Password | `Program.cs:L22` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-L002` | 🔵 | Dev Seed Credentials Hardcoded | `DevSeedData.cs:L11-13` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-L003` | 🔵 | Reset Token in URL Params | `AccountEndpoints.cs:L101` | [data-exposure-review.md](data-exposure-review.md) |
| `DATA-L004` | 🔵 | DevEmailSender Logs Confirmation Links | `DevEmailSender.cs:L17` | [data-exposure-review.md](data-exposure-review.md) |
| `DEP-L001` | 🔵 | TypeScript Tilde Range | `package.json:L37` | [dependency-audit.md](dependency-audit.md) |
| `DEP-L002` | 🔵 | global.json latestMinor rollForward | `global.json:L4` | [dependency-audit.md](dependency-audit.md) |
| `DEP-L003` | 🔵 | Stale Name in package-lock.json | `package-lock.json:L2` | [dependency-audit.md](dependency-audit.md) |
| `INFRA-L001` | 🔵 | No HTTPS in Dev Profile | `launchSettings.json:L4-12` | [infra-review.md](infra-review.md) |
| `INFRA-L002` | 🔵 | No CI/CD Pipeline | N/A | [infra-review.md](infra-review.md) |
| `INFRA-L003` | 🔵 | Tokens Logged at Information Level | `DevEmailSender.cs:L17,23,29` | [infra-review.md](infra-review.md) |
| `INJ-L001` | 🔵 | MCP Guid.Parse Without Error Handling | `CardTools.cs:L49` | [injection-review.md](injection-review.md) |
| `INJ-L002` | 🔵 | No Max-Length on Deck Description | `AppDbContext.cs:L43-48` | [injection-review.md](injection-review.md) |
| `QUAL-L001` | 🔵 | Repeated Auth Boilerplate | `CardEndpoints.cs:L30-31` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-L002` | 🔵 | ToDto Missing DeckCards After Create | `CardEndpoints.cs:L53` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-L003` | 🔵 | DevEmailSender All Environments | `Program.cs:L54` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-L004` | 🔵 | Dead Code in Dashboard Store | `dashboard.ts:L13-18` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-L005` | 🔵 | Unused Store Instantiation | `DashboardView.vue:L13` | [code-quality-review.md](code-quality-review.md) |
| `QUAL-L006` | 🔵 | Swallowed Errors in Add-to-Deck | `CardsView.vue:L78-80` | [code-quality-review.md](code-quality-review.md) |
