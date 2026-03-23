# Nanoid Public IDs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace exposed GUIDs with 12-character alphanumeric nanoid public IDs for cards and decks — GUIDs never leave the server.

**Architecture:** Add a `PublicId` column (string, unique-indexed) to Card and Deck entities. Generate on creation using `NanoidDotNet`. All API endpoints, DTOs, MCP tools, and frontend switch from GUID to nanoid. Internally, GUID remains the primary key for all joins and foreign keys.

**Tech Stack:** NanoidDotNet NuGet package, EF Core migrations, ASP.NET Core Minimal API, Vue 3 + TypeScript

---

## File Map

### Backend — New Files
- `fasolt.Server/Infrastructure/NanoId.cs` — static helper to generate 12-char alphanumeric nanoid

### Backend — Modified Files
- `fasolt.Server/fasolt.Server.csproj` — add NanoidDotNet package
- `fasolt.Server/Domain/Entities/Card.cs` — add `PublicId` property
- `fasolt.Server/Domain/Entities/Deck.cs` — add `PublicId` property
- `fasolt.Server/Infrastructure/Data/AppDbContext.cs` — configure PublicId column (unique index, max length 12)
- `fasolt.Server/Application/Dtos/CardDtos.cs` — change `Guid Id` → `string Id` in CardDto, CardDeckInfoDto
- `fasolt.Server/Application/Dtos/DeckDtos.cs` — change `Guid Id` → `string Id` in DeckDto, DeckDetailDto, DeckCardDto; change `AddCardsToDeckRequest` to use `List<string>`
- `fasolt.Server/Application/Dtos/ReviewDtos.cs` — change `Guid` → `string` in RateCardRequest, RateCardResponse, DueCardDto
- `fasolt.Server/Application/Dtos/BulkCardDtos.cs` — change `Guid? DeckId` → `string? DeckId`
- `fasolt.Server/Application/Services/CardService.cs` — assign PublicId on create, use PublicId in DTOs, resolve nanoid→GUID for lookups
- `fasolt.Server/Application/Services/DeckService.cs` — assign PublicId on create, use PublicId in DTOs, resolve nanoid→GUID for lookups
- `fasolt.Server/Application/Services/SearchService.cs` — join to get PublicId in search results
- `fasolt.Server/Api/Endpoints/CardEndpoints.cs` — route params change from `{id:guid}` to `{id}`, resolve nanoid→GUID
- `fasolt.Server/Api/Endpoints/DeckEndpoints.cs` — route params change from `{id:guid}` to `{id}`, resolve nanoid→GUID
- `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` — deckId query param and RateCard use nanoid
- `fasolt.Server/Api/Endpoints/SearchEndpoints.cs` — change `Guid Id` → `string Id` in result records
- `fasolt.Server/Api/McpTools/CardTools.cs` — change `Guid` params to `string`, resolve nanoid→GUID
- `fasolt.Server/Api/McpTools/DeckTools.cs` — change `Guid` params to `string`, resolve nanoid→GUID

### Frontend — Modified Files
- No type changes needed (already `id: string`)
- No store changes needed (already use string IDs)
- No router changes needed (`:id` param is already a string)

### Migration
- EF Core migration to add `PublicId` column + unique index + backfill

---

### Task 1: Add NanoidDotNet Package and Helper

**Files:**
- Modify: `fasolt.Server/fasolt.Server.csproj`
- Create: `fasolt.Server/Infrastructure/NanoId.cs`

- [ ] **Step 1: Add NanoidDotNet NuGet package**

```bash
cd fasolt.Server && dotnet add package NanoidDotNet
```

- [ ] **Step 2: Create NanoId helper**

```csharp
// fasolt.Server/Infrastructure/NanoId.cs
using NanoidDotNet;

namespace Fasolt.Server.Infrastructure;

public static class NanoIdGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Size = 12;

    public static string New() => Nanoid.Generate(Alphabet, Size);
}
```

- [ ] **Step 3: Verify it compiles**

```bash
cd fasolt.Server && dotnet build
```
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/fasolt.Server.csproj fasolt.Server/Infrastructure/NanoId.cs
git commit -m "feat: add NanoidDotNet package and helper for 12-char alphanumeric IDs"
```

---

### Task 2: Add PublicId to Domain Entities and Configure DB

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Card.cs:7` — add PublicId property after Id
- Modify: `fasolt.Server/Domain/Entities/Deck.cs:7` — add PublicId property after Id
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs:21-54` — add PublicId config

- [ ] **Step 1: Add PublicId to Card entity**

In `fasolt.Server/Domain/Entities/Card.cs`, add after line 7 (`public Guid Id`):

```csharp
public string PublicId { get; set; } = default!;
```

- [ ] **Step 2: Add PublicId to Deck entity**

In `fasolt.Server/Domain/Entities/Deck.cs`, add after line 7 (`public Guid Id`):

```csharp
public string PublicId { get; set; } = default!;
```

- [ ] **Step 3: Configure PublicId in AppDbContext**

In `AppDbContext.OnModelCreating`, add to the Card entity configuration (after the `entity.HasKey` line):

```csharp
entity.Property(e => e.PublicId).HasMaxLength(12).IsRequired();
entity.HasIndex(e => e.PublicId).IsUnique();
```

Add the same to the Deck entity configuration (after the `entity.HasKey` line):

```csharp
entity.Property(e => e.PublicId).HasMaxLength(12).IsRequired();
entity.HasIndex(e => e.PublicId).IsUnique();
```

- [ ] **Step 4: Create EF Core migration**

```bash
cd fasolt.Server && dotnet ef migrations add AddPublicId
```

- [ ] **Step 5: Edit migration to backfill existing rows**

In the generated migration file, after the `AddColumn` for each table, add SQL to backfill existing rows with random nanoids. Before the `CreateIndex` calls, add:

```csharp
// Backfill existing Cards with unique 12-char alphanumeric nanoids
migrationBuilder.Sql("""
    UPDATE "Cards"
    SET "PublicId" = (
        SELECT string_agg(substr('0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz',
            floor(random() * 62)::int + 1, 1), '')
        FROM generate_series(1, 12)
    )
    WHERE "PublicId" IS NULL OR "PublicId" = '';
    """);

// Backfill existing Decks with unique 12-char alphanumeric nanoids
migrationBuilder.Sql("""
    UPDATE "Decks"
    SET "PublicId" = (
        SELECT string_agg(substr('0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz',
            floor(random() * 62)::int + 1, 1), '')
        FROM generate_series(1, 12)
    )
    WHERE "PublicId" IS NULL OR "PublicId" = '';
    """);
```

Note: The AddColumn must set `nullable: true` initially (or provide a default), then the backfill runs, then alter to `nullable: false`. Adjust the migration accordingly:
1. AddColumn with `nullable: true`
2. Run backfill SQL
3. AlterColumn to `nullable: false`
4. CreateIndex (unique)

- [ ] **Step 6: Apply migration and verify**

```bash
docker compose up -d
cd fasolt.Server && dotnet ef database update
```
Expected: Migration applies successfully

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Domain/Entities/Card.cs fasolt.Server/Domain/Entities/Deck.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add PublicId column to Card and Deck entities with unique index"
```

---

### Task 3: Update DTOs to Use String IDs

**Files:**
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/DeckDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/ReviewDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/BulkCardDtos.cs`
- Modify: `fasolt.Server/Api/Endpoints/SearchEndpoints.cs:30-35`

- [ ] **Step 1: Update CardDtos.cs**

Change `CardDto` and `CardDeckInfoDto`:

```csharp
public record CardDto(
    string Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks);
public record CardDeckInfoDto(string Id, string Name);
```

- [ ] **Step 2: Update DeckDtos.cs**

```csharp
public record DeckDto(string Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt);

public record DeckDetailDto(string Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards);

public record DeckCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, DateTimeOffset? DueAt);

public record AddCardsToDeckRequest(List<string> CardIds);
```

- [ ] **Step 3: Update ReviewDtos.cs**

```csharp
public record RateCardRequest(string CardId, int Quality);

public record RateCardResponse(string CardId, double EaseFactor, int Interval, int Repetitions, DateTimeOffset? DueAt, string State);

public record DueCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, double EaseFactor, int Interval, int Repetitions);
```

- [ ] **Step 4: Update BulkCardDtos.cs**

Change `DeckId` from `Guid?` to `string?`:

```csharp
public record BulkCreateCardsRequest(string? SourceFile, string? DeckId, List<BulkCardItem> Cards);
```

- [ ] **Step 5: Update SearchEndpoints.cs result records**

```csharp
public record CardSearchResult(string Id, string Headline, string State);
public record DeckSearchResult(string Id, string Headline, int CardCount);
```

- [ ] **Step 6: Verify it compiles** (it won't fully compile yet — services still pass GUIDs — but DTO shapes should be correct)

```bash
cd fasolt.Server && dotnet build 2>&1 | head -30
```

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Dtos/ fasolt.Server/Api/Endpoints/SearchEndpoints.cs
git commit -m "feat: change all DTOs from Guid to string IDs for nanoid support"
```

---

### Task 4: Update CardService to Use PublicId

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs`

- [ ] **Step 1: Add NanoId import and update CreateCard**

Add `using Fasolt.Server.Infrastructure;` at the top.

In `CreateCard`, set `PublicId = NanoIdGenerator.New()` on the new Card, and change `ToDto` to use `PublicId`:

```csharp
var card = new Card
{
    Id = Guid.NewGuid(),
    PublicId = NanoIdGenerator.New(),
    UserId = userId,
    // ... rest unchanged
};
```

- [ ] **Step 2: Update BulkCreateCards**

Change the method signature from `Guid? deckId` to `string? deckId`:

```csharp
public async Task<BulkCreateResult> BulkCreateCards(string userId, List<BulkCardItem> cards, string? sourceFile, string? deckId)
```

Replace the existing deck validation block at the top with nanoid resolution:

```csharp
Guid? deckGuid = null;
if (deckId is not null)
{
    var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
    if (deck is null) return BulkCreateResult.DeckNotFound();
    deckGuid = deck.Id;
}
```

Then replace **all** references to `deckId.HasValue` → `deckGuid.HasValue` and `deckId.Value` → `deckGuid.Value` for internal DB operations (the DeckCard creation loop and the `deckExists` check are both affected).

In the card creation loop, add `PublicId = NanoIdGenerator.New()`:

```csharp
var card = new Card
{
    Id = Guid.NewGuid(),
    PublicId = NanoIdGenerator.New(),
    UserId = userId,
    // ... rest unchanged
};
```

Update the `createdDtos` mapping to use `c.PublicId` and `deckId` (the nanoid string) for DTO output:

```csharp
var createdDtos = created.Select(c => new CardDto(
    c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back,
    c.State, c.CreatedAt,
    deckId is not null
        ? [new CardDeckInfoDto(deckId, "")]
        : [])).ToList();
```

- [ ] **Step 3: Update GetCard to resolve by nanoid**

Change signature from `Guid cardId` to `string publicId`:

```csharp
public async Task<CardDto?> GetCard(string userId, string publicId)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

    return card is null ? null : ToDto(card);
}
```

- [ ] **Step 4: Update UpdateCard to resolve by nanoid**

Change signature from `Guid cardId` to `string publicId`:

```csharp
public async Task<CardDto?> UpdateCard(string userId, string publicId, string front, string back)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);
    // ... rest unchanged
}
```

- [ ] **Step 5: Update UpdateCardFields to resolve by nanoid**

Change signature from `Guid cardId` to `string publicId`:

```csharp
public async Task<UpdateCardResult> UpdateCardFields(string userId, string publicId, UpdateCardFieldsRequest req)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);
    // ... rest unchanged
}
```

- [ ] **Step 6: Update DeleteCard to resolve by nanoid**

```csharp
public async Task<bool> DeleteCard(string userId, string publicId)
{
    var deleted = await db.Cards
        .Where(c => c.PublicId == publicId && c.UserId == userId)
        .ExecuteDeleteAsync();
    return deleted > 0;
}
```

- [ ] **Step 7: Update DeleteCards (bulk) to resolve by nanoid**

Change from `List<Guid>` to `List<string>`:

```csharp
public async Task<int> DeleteCards(string userId, List<string> publicIds)
{
    return await db.Cards
        .Where(c => c.UserId == userId && publicIds.Contains(c.PublicId))
        .ExecuteDeleteAsync();
}
```

- [ ] **Step 8: Update ListCards signature and deckId resolution**

Change the method signature from `Guid? deckId` to `string? deckId`:

```csharp
public async Task<PaginatedResponse<CardDto>> ListCards(string userId, string? sourceFile, string? deckId, int? limit, string? after)
```

Replace the deckId filtering block with nanoid resolution:

```csharp
if (deckId is not null)
{
    var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
    if (deck is not null)
        query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
}
```

- [ ] **Step 9: Update ListCards pagination cursor**

The cursor should use `PublicId` instead of `Id.ToString()`. Change the cursor resolution:

```csharp
if (after is not null)
{
    var cursor = await db.Cards.Where(c => c.PublicId == after && c.UserId == userId)
        .Select(c => new { c.CreatedAt, c.Id }).FirstOrDefaultAsync();
    if (cursor is not null)
        query = query.Where(c => c.CreatedAt < cursor.CreatedAt ||
            (c.CreatedAt == cursor.CreatedAt && c.Id.CompareTo(cursor.Id) > 0));
}
```

And the next cursor:

```csharp
var nextCursor = hasMore ? cards[^1].Id : null;
```

(Since `cards` is now `List<CardDto>` with `string Id` = PublicId, this naturally uses the nanoid.)

- [ ] **Step 10: Update ToDto to use PublicId**

```csharp
private static CardDto ToDto(Card c) =>
    new(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
        c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name)).ToList());
```

- [ ] **Step 11: Update ListCards LINQ projection**

The `Select` in `ListCards` constructs `CardDto` inline. Update it to use `PublicId`:

```csharp
.Select(c => new CardDto(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
    c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name)).ToList()))
```

- [ ] **Step 12: Verify it compiles**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 13: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs
git commit -m "feat: CardService uses PublicId for all external-facing operations"
```

---

### Task 5: Update DeckService to Use PublicId

**Files:**
- Modify: `fasolt.Server/Application/Services/DeckService.cs`

- [ ] **Step 1: Add NanoId import and update CreateDeck**

Add `using Fasolt.Server.Infrastructure;` at the top.

```csharp
var deck = new Deck
{
    Id = Guid.NewGuid(),
    PublicId = NanoIdGenerator.New(),
    UserId = userId,
    // ... rest unchanged
};
```

Return DTO with `deck.PublicId`:

```csharp
return new DeckDto(deck.PublicId, deck.Name, deck.Description, 0, 0, deck.CreatedAt);
```

- [ ] **Step 2: Update ListDecks**

Change the `Select` projection to use `PublicId`:

```csharp
.Select(d => new DeckDto(
    d.PublicId,
    d.Name,
    d.Description,
    d.Cards.Count,
    d.Cards.Count(dc => dc.Card.DueAt == null || dc.Card.DueAt <= now),
    d.CreatedAt))
```

- [ ] **Step 3: Update GetDeck to resolve by nanoid**

Change signature from `Guid deckId` to `string publicId`:

```csharp
public async Task<DeckDetailDto?> GetDeck(string userId, string publicId)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

    if (deck is null) return null;

    var now = DateTimeOffset.UtcNow;

    var cards = await db.DeckCards
        .Where(dc => dc.DeckId == deck.Id)
        .OrderBy(dc => dc.Card.DueAt)
        .Select(dc => new DeckCardDto(dc.Card.PublicId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt))
        .ToListAsync();

    var dueCount = cards.Count(c => c.DueAt == null || c.DueAt <= now);

    return new DeckDetailDto(deck.PublicId, deck.Name, deck.Description, cards.Count, dueCount, cards);
}
```

- [ ] **Step 4: Update UpdateDeck to resolve by nanoid**

Change signature from `Guid deckId` to `string publicId`:

```csharp
public async Task<DeckDto?> UpdateDeck(string userId, string publicId, string name, string? description)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

    if (deck is null) return null;

    deck.Name = name.Trim();
    deck.Description = description?.Trim();
    await db.SaveChangesAsync();

    var now = DateTimeOffset.UtcNow;
    var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
    var dueCount = await db.DeckCards.CountAsync(dc =>
        dc.DeckId == deck.Id && (dc.Card.DueAt == null || dc.Card.DueAt <= now));

    return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt);
}
```

- [ ] **Step 5: Update DeleteDeck to resolve by nanoid**

```csharp
public async Task<DeleteDeckResult> DeleteDeck(string userId, string publicId, bool deleteCards = false)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

    if (deck is null) return new DeleteDeckResult(false, 0);

    var cardIds = deleteCards
        ? await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id)
            .Select(dc => dc.CardId)
            .ToListAsync()
        : [];

    db.Decks.Remove(deck);
    await db.SaveChangesAsync();

    var deletedCardCount = 0;
    if (cardIds.Count > 0)
    {
        deletedCardCount = await db.Cards
            .Where(c => cardIds.Contains(c.Id) && c.UserId == userId)
            .ExecuteDeleteAsync();
    }

    return new DeleteDeckResult(true, deletedCardCount);
}
```

- [ ] **Step 6: Update AddCards to resolve nanoid IDs**

Change from `Guid deckId, List<Guid> cardIds` to `string deckPublicId, List<string> cardPublicIds`:

```csharp
public async Task<AddCardsResult> AddCards(string userId, string deckPublicId, List<string> cardPublicIds)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

    if (deck is null) return AddCardsResult.DeckNotFound;

    var userCards = await db.Cards
        .Where(c => c.UserId == userId && cardPublicIds.Contains(c.PublicId))
        .Select(c => new { c.Id, c.PublicId })
        .ToListAsync();

    if (userCards.Count != cardPublicIds.Count)
        return AddCardsResult.CardsNotFound;

    var userCardGuids = userCards.Select(c => c.Id).ToList();

    var existingCardIds = await db.DeckCards
        .Where(dc => dc.DeckId == deck.Id && userCardGuids.Contains(dc.CardId))
        .Select(dc => dc.CardId)
        .ToListAsync();

    var newCardIds = userCardGuids.Except(existingCardIds);

    foreach (var cardId in newCardIds)
    {
        db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = cardId });
    }

    await db.SaveChangesAsync();
    return AddCardsResult.Success;
}
```

- [ ] **Step 7: Update RemoveCard to resolve nanoid IDs**

```csharp
public async Task<RemoveCardResult> RemoveCard(string userId, string deckPublicId, string cardPublicId)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

    if (deck is null) return RemoveCardResult.DeckNotFound;

    var card = await db.Cards
        .FirstOrDefaultAsync(c => c.PublicId == cardPublicId && c.UserId == userId);

    if (card is null) return RemoveCardResult.CardNotFound;

    var deckCard = await db.DeckCards
        .FirstOrDefaultAsync(dc => dc.DeckId == deck.Id && dc.CardId == card.Id);

    if (deckCard is null) return RemoveCardResult.CardNotFound;

    db.DeckCards.Remove(deckCard);
    await db.SaveChangesAsync();
    return RemoveCardResult.Success;
}
```

- [ ] **Step 8: Verify it compiles**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 9: Commit**

```bash
git add fasolt.Server/Application/Services/DeckService.cs
git commit -m "feat: DeckService uses PublicId for all external-facing operations"
```

---

### Task 6: Update SearchService to Return PublicId

**Files:**
- Modify: `fasolt.Server/Application/Services/SearchService.cs`

- [ ] **Step 1: Update card search SQL to return PublicId**

Change the SQL to select `"PublicId"` instead of `"Id"`:

```csharp
var cards = await db.Database
    .SqlQueryRaw<CardSearchResult>("""
        SELECT c."PublicId" AS "Id",
               ts_headline('english', c."Front", plainto_tsquery('english', {0}),
                   'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
               c."State"
        FROM "Cards" c
        WHERE c."UserId" = {1}
          AND c."SearchVector" @@ plainto_tsquery('english', {0})
        ORDER BY ts_rank(c."SearchVector", plainto_tsquery('english', {0})) DESC
        LIMIT 10
        """, term, userId)
    .ToListAsync();
```

- [ ] **Step 2: Update deck search SQL to return PublicId**

```csharp
var decks = await db.Database
    .SqlQueryRaw<DeckSearchResult>("""
        SELECT d."PublicId" AS "Id",
               ts_headline('english', d."Name", plainto_tsquery('english', {0}),
                   'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
               (SELECT COUNT(*) FROM "DeckCards" dc
                INNER JOIN "Cards" card ON dc."CardId" = card."Id"
                WHERE dc."DeckId" = d."Id") AS "CardCount"
        FROM "Decks" d
        WHERE d."UserId" = {1}
          AND d."SearchVector" @@ plainto_tsquery('english', {0})
        ORDER BY ts_rank(d."SearchVector", plainto_tsquery('english', {0})) DESC
        LIMIT 10
        """, term, userId)
    .ToListAsync();
```

- [ ] **Step 3: Verify it compiles**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Services/SearchService.cs
git commit -m "feat: search results return PublicId instead of GUID"
```

---

### Task 7: Update API Endpoints

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`

- [ ] **Step 1: Update CardEndpoints**

Change route constraints from `{id:guid}` to `{id}` and parameter types from `Guid` to `string`:

```csharp
group.MapGet("/{id}", GetById);
group.MapPut("/{id}", Update);
group.MapDelete("/{id}", Delete);
```

Update handler signatures:

```csharp
private static async Task<IResult> GetById(
    string id,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    CardService cardService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var dto = await cardService.GetCard(user.Id, id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}
```

Similarly for `Update` (change `Guid id` → `string id`) and `Delete` (change `Guid id` → `string id`).

Update `Create` response location: `$"/api/cards/{dto.Id}"` (already correct since dto.Id is now the nanoid string).

Update `List` handler: change `Guid? deckId` → `string? deckId` parameter.

- [ ] **Step 2: Update DeckEndpoints**

Change route constraints from `{id:guid}` to `{id}` and `{cardId:guid}` to `{cardId}`:

```csharp
group.MapGet("/{id}", GetById);
group.MapPut("/{id}", Update);
group.MapDelete("/{id}", Delete);
group.MapPost("/{id}/cards", AddCards);
group.MapDelete("/{id}/cards/{cardId}", RemoveCard);
```

Update all handler signatures from `Guid id` → `string id`, `Guid cardId` → `string cardId`.

- [ ] **Step 3: Update ReviewEndpoints**

Change `GetDueCards` parameter from `Guid? deckId` to `string? deckId`. Resolve to GUID internally:

```csharp
private static async Task<IResult> GetDueCards(
    ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, int limit = 50, string? deckId = null)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var take = Math.Clamp(limit, 1, 200);
    var now = DateTimeOffset.UtcNow;
    var query = db.Cards
        .Where(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));

    if (deckId is not null)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == user.Id);
        if (deck is null) return Results.NotFound();
        query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
    }

    var cards = await query
        .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
        .ThenBy(c => c.CreatedAt)
        .Take(take)
        .Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State, c.EaseFactor, c.Interval, c.Repetitions))
        .ToListAsync();

    return Results.Ok(cards);
}
```

Update `RateCard` to resolve nanoid → card:

```csharp
var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == user.Id);
```

And the response:

```csharp
return Results.Ok(new RateCardResponse(card.PublicId, card.EaseFactor, card.Interval, card.Repetitions, card.DueAt, card.State));
```

- [ ] **Step 4: Verify it compiles**

```bash
cd fasolt.Server && dotnet build
```
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/Endpoints/CardEndpoints.cs fasolt.Server/Api/Endpoints/DeckEndpoints.cs fasolt.Server/Api/Endpoints/ReviewEndpoints.cs
git commit -m "feat: API endpoints use nanoid public IDs instead of GUIDs"
```

---

### Task 8: Update MCP Tools

**Files:**
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs`
- Modify: `fasolt.Server/Api/McpTools/DeckTools.cs`

- [ ] **Step 1: Update CardTools**

Change all `Guid` parameters to `string`:

- `ListCards`: `Guid? deckId` → `string? deckId`
- `CreateCards`: `Guid? deckId` → `string? deckId`
- `DeleteCards`: `List<Guid> cardIds` → `List<string> cardIds`
- `UpdateCard`: `Guid? cardId` → `string? cardId`

Update descriptions to say "ID" instead of "GUID" where applicable.

In `CreateCards`, fix the `deckUrl` construction — change `deckId.HasValue` → `deckId is not null` and `deckId.Value` → `deckId` (since `string?` doesn't have `.HasValue`/`.Value`):

```csharp
if (deckId is not null)
{
    var request = httpContextAccessor.HttpContext!.Request;
    var baseUrl = $"{request.Scheme}://{request.Host}";
    return JsonSerializer.Serialize(new
    {
        response.Created,
        response.Skipped,
        deckUrl = $"{baseUrl}/decks/{deckId}",
    });
}
```

- [ ] **Step 2: Update DeckTools**

Change all `Guid` parameters to `string`:

- `AddCardsToDeck`: `Guid deckId` → `string deckId`, `List<Guid> cardIds` → `List<string> cardIds`
- `RemoveCardsFromDeck`: `Guid deckId` → `string deckId`, `List<Guid> cardIds` → `List<string> cardIds`
- `MoveCards`: `Guid fromDeckId` → `string fromDeckId`, `Guid toDeckId` → `string toDeckId`, `List<Guid> cardIds` → `List<string> cardIds`
- `DeleteDeck`: `Guid deckId` → `string deckId`

- [ ] **Step 3: Verify it compiles**

```bash
cd fasolt.Server && dotnet build
```
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/McpTools/CardTools.cs fasolt.Server/Api/McpTools/DeckTools.cs
git commit -m "feat: MCP tools use nanoid public IDs instead of GUIDs"
```

---

### Task 9: Full Stack Verification

**Files:** None (testing only)

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh
```

- [ ] **Step 2: Test API manually — create a card**

```bash
# Login as dev user
curl -s -c cookies.txt -X POST http://localhost:8080/api/identity/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"dev@fasolt.local","password":"Dev1234!"}'

# Create a card
curl -s -b cookies.txt -X POST http://localhost:8080/api/cards \
  -H 'Content-Type: application/json' \
  -d '{"front":"Test nanoid","back":"Should return short ID"}' | jq .
```

Expected: Response `id` field is a 12-character alphanumeric string (not a GUID).

- [ ] **Step 3: Test API — list cards, verify IDs are nanoids**

```bash
curl -s -b cookies.txt http://localhost:8080/api/cards | jq '.items[0].id'
```

Expected: A 12-char string like `"a1B2c3D4e5F6"`

- [ ] **Step 4: Test API — get card by nanoid**

```bash
CARD_ID=$(curl -s -b cookies.txt http://localhost:8080/api/cards | jq -r '.items[0].id')
curl -s -b cookies.txt http://localhost:8080/api/cards/$CARD_ID | jq .id
```

Expected: Same nanoid returned

- [ ] **Step 5: Test API — create and retrieve deck**

```bash
DECK=$(curl -s -b cookies.txt -X POST http://localhost:8080/api/decks \
  -H 'Content-Type: application/json' \
  -d '{"name":"Test Deck"}')
echo $DECK | jq .id
DECK_ID=$(echo $DECK | jq -r .id)
curl -s -b cookies.txt http://localhost:8080/api/decks/$DECK_ID | jq .id
```

Expected: 12-char nanoid in both responses

- [ ] **Step 6: Run Playwright browser tests**

Test the full UI flow using Playwright MCP:
1. Navigate to login page, login as dev user
2. Navigate to /cards — verify card table loads
3. Click a card — verify URL contains nanoid (not GUID)
4. Navigate to /decks — verify deck list loads
5. Click a deck — verify URL contains nanoid
6. Start a review session — verify it works

- [ ] **Step 7: Commit (if any fixes were needed)**

```bash
git add -A
git commit -m "fix: address issues found during nanoid integration testing"
```
