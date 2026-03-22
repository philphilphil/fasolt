using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards").RequireAuthorization();

        group.MapPost("/", Create);
        group.MapPost("/bulk", BulkCreate);
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Create(
        CreateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Front and back are required."]
            });

        var card = new Card
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceFile = request.SourceFile?.Trim(),
            SourceHeading = request.SourceHeading,
            Front = request.Front,
            Back = request.Back,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Cards.Add(card);
        await db.SaveChangesAsync();

        return Results.Created($"/api/cards/{card.Id}", ToDto(card));
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        string? sourceFile = null,
        Guid? deckId = null,
        int? limit = null,
        string? after = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var take = Math.Clamp(limit ?? 50, 1, 200);

        IQueryable<Card> query = db.Cards
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id);

        if (sourceFile is not null)
            query = query.Where(c => c.SourceFile == sourceFile);

        if (deckId.HasValue)
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deckId.Value));

        if (Guid.TryParse(after, out var afterId))
        {
            var cursor = await db.Cards.Where(c => c.Id == afterId && c.UserId == user.Id)
                .Select(c => new { c.CreatedAt, c.Id }).FirstOrDefaultAsync();
            if (cursor is not null)
                query = query.Where(c => c.CreatedAt < cursor.CreatedAt ||
                    (c.CreatedAt == cursor.CreatedAt && c.Id.CompareTo(cursor.Id) > 0));
        }

        var cards = await query
            .Take(take + 1)
            .Select(c => new CardDto(c.Id, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
                c.DeckCards.Select(dc => new CardDeckInfoDto(dc.DeckId, dc.Deck.Name)).ToList()))
            .ToListAsync();

        var hasMore = cards.Count > take;
        if (hasMore) cards = cards[..take];
        var nextCursor = hasMore ? cards[^1].Id.ToString() : null;

        return Results.Ok(new PaginatedResponse<CardDto>(cards, hasMore, nextCursor));
    }

    private static async Task<IResult> GetById(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        return Results.Ok(ToDto(card));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Front and back are required."]
            });

        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        card.Front = request.Front;
        card.Back = request.Back;
        await db.SaveChangesAsync();

        return Results.Ok(ToDto(card));
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        card.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> BulkCreate(
        BulkCreateCardsRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (request.Cards is null || request.Cards.Count == 0)
            return Results.BadRequest(new { error = "validation_error", message = "Cards array is required and must not be empty" });

        if (request.Cards.Count > 100)
            return Results.BadRequest(new { error = "validation_error", message = "Maximum 100 cards per request" });

        // Validate all cards first (atomic — if any fail, none are created)
        var validationErrors = new List<object>();
        for (var i = 0; i < request.Cards.Count; i++)
        {
            var c = request.Cards[i];
            if (string.IsNullOrWhiteSpace(c.Front))
                validationErrors.Add(new { field = $"cards[{i}].front", message = "Front is required" });
            if (string.IsNullOrWhiteSpace(c.Back))
                validationErrors.Add(new { field = $"cards[{i}].back", message = "Back is required" });
        }
        if (validationErrors.Count > 0)
            return Results.BadRequest(new { error = "validation_error", message = "Validation failed", details = validationErrors });

        // Validate deckId if provided
        if (request.DeckId.HasValue)
        {
            var deckExists = await db.Decks
                .AnyAsync(d => d.Id == request.DeckId.Value && d.UserId == user.Id);
            if (!deckExists)
                return Results.BadRequest(new { error = "validation_error", message = "Deck not found or does not belong to you" });
        }

        // Check for duplicates — group by effective sourceFile per card, then check (UserId, SourceFile, Front)
        // For cards with a sourceFile, check (UserId, SourceFile, Front); for null sourceFile, check (UserId, Front) where SourceFile is null
        var fronts = request.Cards.Select(c => c.Front.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestSourceFile = request.SourceFile?.Trim();

        // Collect all (sourceFile, front) pairs to check for duplicates
        var cardsWithSource = request.Cards
            .Where(c => (c.SourceFile?.Trim() ?? requestSourceFile) is not null)
            .Select(c => new { SourceFile = (c.SourceFile?.Trim() ?? requestSourceFile)!, Front = c.Front.Trim() })
            .ToList();

        var cardsWithoutSource = request.Cards
            .Where(c => (c.SourceFile?.Trim() ?? requestSourceFile) is null)
            .Select(c => c.Front.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Query existing cards for duplicate detection
        var existingWithSource = cardsWithSource.Count > 0
            ? await db.Cards
                .Where(c => c.UserId == user.Id && c.SourceFile != null && fronts.Contains(c.Front))
                .Select(c => new { c.SourceFile, c.Front })
                .ToListAsync()
            : [];

        var existingWithoutSource = cardsWithoutSource.Count > 0
            ? await db.Cards
                .Where(c => c.UserId == user.Id && c.SourceFile == null && fronts.Contains(c.Front))
                .Select(c => c.Front)
                .ToListAsync()
            : [];

        var existingWithSourceSet = existingWithSource
            .Select(x => (x.SourceFile!, x.Front))
            .ToHashSet();
        var existingWithoutSourceSet = existingWithoutSource.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<Card>();
        var skipped = new List<SkippedCardDto>();

        foreach (var item in request.Cards)
        {
            var trimmedFront = item.Front.Trim();
            var effectiveSourceFile = item.SourceFile?.Trim() ?? requestSourceFile;

            // Check for duplicate in DB
            bool isDuplicate = effectiveSourceFile is not null
                ? existingWithSourceSet.Contains((effectiveSourceFile, trimmedFront))
                : existingWithoutSourceSet.Contains(trimmedFront);

            if (isDuplicate)
            {
                skipped.Add(new SkippedCardDto(trimmedFront, "Card with same front text already exists"));
                continue;
            }

            // Also skip duplicates within the same batch
            if (created.Any(c => c.Front.Equals(trimmedFront, StringComparison.OrdinalIgnoreCase) &&
                (c.SourceFile ?? "") == (effectiveSourceFile ?? "")))
            {
                skipped.Add(new SkippedCardDto(trimmedFront, "Duplicate within batch"));
                continue;
            }

            var card = new Card
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SourceFile = effectiveSourceFile,
                SourceHeading = item.SourceHeading,
                Front = trimmedFront,
                Back = item.Back.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                EaseFactor = 2.5,
                Interval = 0,
                Repetitions = 0,
                State = "new",
            };
            db.Cards.Add(card);
            created.Add(card);
        }

        // Add to deck if specified
        if (request.DeckId.HasValue)
        {
            foreach (var card in created)
            {
                db.DeckCards.Add(new DeckCard
                {
                    DeckId = request.DeckId.Value,
                    CardId = card.Id,
                });
            }
        }

        await db.SaveChangesAsync();

        var createdDtos = created.Select(c => new CardDto(
            c.Id, c.SourceFile, c.SourceHeading, c.Front, c.Back,
            c.State, c.CreatedAt,
            request.DeckId.HasValue
                ? [new CardDeckInfoDto(request.DeckId.Value, "")]
                : [])).ToList();

        return Results.Created("/api/cards/bulk", new BulkCreateCardsResponse(createdDtos, skipped));
    }

    private static CardDto ToDto(Card c) =>
        new(c.Id, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
            c.DeckCards.Select(dc => new CardDeckInfoDto(dc.DeckId, dc.Deck.Name)).ToList());
}
