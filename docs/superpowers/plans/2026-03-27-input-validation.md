# Input Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix search LIKE pattern injection and add field length limits to card creation/update.

**Architecture:** Escape LIKE metacharacters in SearchService, add validation in CardService, add HasMaxLength constraints in AppDbContext, generate migration.

**Tech Stack:** .NET 10, EF Core, Npgsql, xUnit + FluentAssertions

---

## Stream A: Search LIKE Pattern Escaping (INJ-H001)

### Task A1: Fix SearchService and add tests

**Files:**
- Modify: `fasolt.Server/Application/Services/SearchService.cs:L14` — escape wildcards
- Modify: `fasolt.Tests/SearchServiceTests.cs` — add wildcard tests

- [ ] **Step 1: Fix SearchService.cs**

Replace line 14:
```csharp
var pattern = $"%{query.Trim()}%";
```
With:
```csharp
var escaped = query.Trim()
    .Replace("\\", "\\\\")
    .Replace("%", "\\%")
    .Replace("_", "\\_");
var pattern = $"%{escaped}%";
```

And update the ILike calls to include the escape character. Replace:
```csharp
(EF.Functions.ILike(c.Front, pattern) || EF.Functions.ILike(c.Back, pattern)))
```
With:
```csharp
(EF.Functions.ILike(c.Front, pattern, "\\") || EF.Functions.ILike(c.Back, pattern, "\\")))
```

And replace:
```csharp
EF.Functions.ILike(d.Name, pattern))
```
With:
```csharp
EF.Functions.ILike(d.Name, pattern, "\\"))
```

- [ ] **Step 2: Add wildcard escaping tests to SearchServiceTests.cs**

Append these tests to the existing `SearchServiceTests` class:

```csharp
[Fact]
public async Task Search_EscapesPercentWildcard()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var searchSvc = new SearchService(db);

    await cardSvc.CreateCard(UserId, "100% correct", "Always right", null, null);
    await cardSvc.CreateCard(UserId, "Unrelated card", "Nothing here", null, null);

    var result = await searchSvc.Search(UserId, "100%");

    result.Cards.Should().ContainSingle(c => c.Headline.Contains("100%"));
}

[Fact]
public async Task Search_EscapesUnderscoreWildcard()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var searchSvc = new SearchService(db);

    await cardSvc.CreateCard(UserId, "snake_case naming", "Use underscores", null, null);
    await cardSvc.CreateCard(UserId, "snakeXcase naming", "Not underscore", null, null);

    var result = await searchSvc.Search(UserId, "snake_case");

    result.Cards.Should().ContainSingle(c => c.Headline.Contains("snake_case"));
}
```

- [ ] **Step 3: Build and test**

Run: `dotnet build fasolt.Server && dotnet test fasolt.Tests --filter SearchService`
Expected: All pass including new wildcard tests

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Services/SearchService.cs fasolt.Tests/SearchServiceTests.cs
git commit -m "security: escape LIKE wildcards in search queries (INJ-H001)"
```

---

## Stream B: Field Length Limits (INJ-H002)

### Task B1: Add validation to CardService

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs` — add validation helper, use in CreateCard, BulkCreateCards, UpdateCard, ApplyCardFieldUpdates

- [ ] **Step 1: Add validation constants and helper to CardService**

Add at the top of the `CardService` class (inside, before `CreateCard`):

```csharp
public const int MaxFrontLength = 10_000;
public const int MaxBackLength = 50_000;
public const int MaxSourceHeadingLength = 255;

public static List<string> ValidateCardFields(string? front, string? back, string? sourceHeading)
{
    var errors = new List<string>();
    if (front is not null && front.Length > MaxFrontLength)
        errors.Add($"Front text exceeds maximum length of {MaxFrontLength} characters.");
    if (back is not null && back.Length > MaxBackLength)
        errors.Add($"Back text exceeds maximum length of {MaxBackLength} characters.");
    if (sourceHeading is not null && sourceHeading.Length > MaxSourceHeadingLength)
        errors.Add($"Source heading exceeds maximum length of {MaxSourceHeadingLength} characters.");
    return errors;
}
```

- [ ] **Step 2: Add validation to CreateCard**

At the start of `CreateCard`, before creating the entity:

```csharp
var errors = ValidateCardFields(front, back, sourceHeading);
if (errors.Count > 0)
    throw new ValidationException(string.Join(" ", errors));
```

Add the using at the top of the file:
```csharp
using System.ComponentModel.DataAnnotations;
```

- [ ] **Step 3: Add validation to BulkCreateCards**

Inside the `foreach (var item in cards)` loop, after the duplicate checks and before creating the entity (after the `createdKeys.Add` check), add:

```csharp
var itemErrors = ValidateCardFields(item.Front, item.Back, item.SourceHeading);
if (itemErrors.Count > 0)
{
    skipped.Add(new SkippedCardDto(trimmedFront, string.Join(" ", itemErrors)));
    continue;
}
```

- [ ] **Step 4: Add validation to UpdateCard**

In `UpdateCard`, after the null check for `card`, add:

```csharp
var errors = ValidateCardFields(request.Front, request.Back, request.SourceHeading);
if (errors.Count > 0)
    throw new ValidationException(string.Join(" ", errors));
```

- [ ] **Step 5: Add validation to ApplyCardFieldUpdates**

In `ApplyCardFieldUpdates`, after computing `effectiveFront` and `effectiveSourceFile`, add:

```csharp
var errors = ValidateCardFields(req.NewFront, req.NewBack, req.NewSourceHeading);
if (errors.Count > 0)
    throw new ValidationException(string.Join(" ", errors));
```

- [ ] **Step 6: Build**

Run: `dotnet build fasolt.Server`
Expected: Success

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs
git commit -m "security: add field length validation to CardService (INJ-H002)"
```

### Task B2: Add DB constraints and migration

**Files:**
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs:L31-32` — add HasMaxLength
- New migration via `dotnet ef`

- [ ] **Step 1: Add HasMaxLength constraints in AppDbContext**

Replace:
```csharp
entity.Property(e => e.Front).IsRequired();
entity.Property(e => e.Back).IsRequired();
```
With:
```csharp
entity.Property(e => e.Front).HasMaxLength(10_000).IsRequired();
entity.Property(e => e.Back).HasMaxLength(50_000).IsRequired();
```

And after the `entity.Property(e => e.SourceFile).HasMaxLength(255);` line, add:
```csharp
entity.Property(e => e.SourceHeading).HasMaxLength(255);
```

- [ ] **Step 2: Generate migration**

Run: `dotnet ef migrations add AddCardFieldLengthLimits --project fasolt.Server`
Expected: Migration file created

- [ ] **Step 3: Apply migration to dev DB**

Run: `dotnet ef database update --project fasolt.Server`
Expected: Success

- [ ] **Step 4: Build and run all tests**

Run: `dotnet build fasolt.Server && dotnet test fasolt.Tests`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "security: add HasMaxLength DB constraints for card fields (INJ-H002)"
```

### Task B3: Add CardService validation tests

**Files:**
- Modify: `fasolt.Tests/CardServiceTests.cs` — add length validation tests

- [ ] **Step 1: Add validation tests**

Append to `CardServiceTests`:

```csharp
[Fact]
public async Task CreateCard_RejectsOversizedFront()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var longFront = new string('x', CardService.MaxFrontLength + 1);

    var act = () => svc.CreateCard(UserId, longFront, "Back", null, null);

    await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
        .WithMessage("*Front*maximum*");
}

[Fact]
public async Task CreateCard_RejectsOversizedBack()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var longBack = new string('x', CardService.MaxBackLength + 1);

    var act = () => svc.CreateCard(UserId, "Front", longBack, null, null);

    await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
        .WithMessage("*Back*maximum*");
}

[Fact]
public async Task CreateCard_RejectsOversizedSourceHeading()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var longHeading = new string('x', CardService.MaxSourceHeadingLength + 1);

    var act = () => svc.CreateCard(UserId, "Front", "Back", "file.md", longHeading);

    await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
        .WithMessage("*Source heading*maximum*");
}

[Fact]
public async Task CreateCard_AcceptsMaxLengthFields()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var front = new string('x', CardService.MaxFrontLength);
    var back = new string('x', CardService.MaxBackLength);
    var heading = new string('x', CardService.MaxSourceHeadingLength);

    var card = await svc.CreateCard(UserId, front, back, "file.md", heading);

    card.Front.Should().HaveLength(CardService.MaxFrontLength);
    card.Back.Should().HaveLength(CardService.MaxBackLength);
    card.SourceHeading.Should().HaveLength(CardService.MaxSourceHeadingLength);
}

[Fact]
public async Task BulkCreateCards_SkipsOversizedCards()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var cards = new List<BulkCardItem>
    {
        new("Normal front", "Normal back"),
        new(new string('x', CardService.MaxFrontLength + 1), "Back"),
    };

    var result = await svc.BulkCreateCards(UserId, cards, null, null);

    result.Response!.Created.Should().HaveCount(1);
    result.Response.Skipped.Should().HaveCount(1);
    result.Response.Skipped[0].Reason.Should().Contain("Front");
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test fasolt.Tests --filter CardService`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/CardServiceTests.cs
git commit -m "test: add field length validation tests for CardService"
```
