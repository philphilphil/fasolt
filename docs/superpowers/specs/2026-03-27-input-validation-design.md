# Input Validation: Search Escaping & Field Length Limits

**Issue:** #35 — Add input validation: search wildcard escaping and field length limits
**Date:** 2026-03-27

## Problem

Two input validation gaps:
1. Search LIKE pattern injection — `%` and `_` in user queries aren't escaped, enabling targeted DoS via full-table scans
2. No length limits on card `Front`, `Back`, `SourceHeading` — allows storage abuse via megabyte-sized strings

## Design

### INJ-H001: Search LIKE Pattern Escaping

In `SearchService.Search()`, escape LIKE metacharacters before wrapping in wildcards:

```csharp
var escaped = query.Trim()
    .Replace("\\", "\\\\")
    .Replace("%", "\\%")
    .Replace("_", "\\_");
var pattern = $"%{escaped}%";
```

Pass escape char to `ILike` calls so Postgres applies it:

```csharp
EF.Functions.ILike(c.Front, pattern, "\\")
```

### INJ-H002: Field Length Limits

**Limits:**
- `Front`: 10,000 chars
- `Back`: 50,000 chars
- `SourceHeading`: 255 chars (match `SourceFile`)
- `FrontSvg`/`BackSvg`: already limited by SvgSanitizer (1MB) — no change

**Service-level validation** in `CardService.CreateCard()` and `CardService.UpdateCard()` — return validation error for oversized fields. Bulk create applies the same limits per item.

**DB defense-in-depth** in `AppDbContext.OnModelCreating()`:
- `entity.Property(e => e.Front).HasMaxLength(10_000)`
- `entity.Property(e => e.Back).HasMaxLength(50_000)`
- `entity.Property(e => e.SourceHeading).HasMaxLength(255)`

Requires an EF Core migration.

### Tests

- `SearchServiceTests`: query containing `%` and `_` treated as literal characters
- `CardServiceTests`: oversized Front/Back/SourceHeading rejected with clear error

## Non-Goals

- No changes to SVG limits (already handled by SvgSanitizer)
- No changes to API response format — validation errors use existing 400 pattern
