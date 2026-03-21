using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Application.Services;
using SpacedMd.Server.Domain.Entities;
using SpacedMd.Server.Infrastructure.Data;

namespace SpacedMd.Server.Api.Endpoints;

public static class CardEndpoints
{
    private static readonly string[] ValidCardTypes = ["file", "section", "custom"];

    public static void MapCardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards").RequireAuthorization();

        group.MapPost("/", Create);
        group.MapPost("/bulk", BulkCreate);
        group.MapGet("/", List);
        group.MapGet("/extract", Extract);
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

        if (!ValidCardTypes.Contains(request.CardType))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cardType"] = ["Card type must be 'file', 'section', or 'custom'."]
            });

        if (request.CardType is "file" or "section")
        {
            if (request.FileId is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["fileId"] = ["File ID is required for file and section cards."]
                });

            var fileExists = await db.MarkdownFiles
                .AnyAsync(f => f.Id == request.FileId && f.UserId == user.Id);
            if (!fileExists) return Results.NotFound();
        }

        var card = new Card
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FileId = request.CardType == "custom" ? null : request.FileId,
            SourceHeading = request.SourceHeading,
            Front = request.Front,
            Back = request.Back,
            CardType = request.CardType,
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
        Guid? fileId = null,
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

        if (fileId.HasValue)
            query = query.Where(c => c.FileId == fileId.Value);

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
            .Select(c => new CardDto(c.Id, c.FileId, c.SourceHeading, c.Front, c.Back, c.CardType, c.State, c.CreatedAt,
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

    private static async Task<IResult> Extract(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        Guid? fileId = null,
        string? heading = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (fileId is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["fileId"] = ["File ID is required."]
            });

        var file = await db.MarkdownFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == user.Id);

        if (file is null) return Results.NotFound();

        string rawContent;
        string defaultFront;

        if (string.IsNullOrWhiteSpace(heading))
        {
            defaultFront = ContentExtractor.GetFirstH1(file.Content) ?? file.FileName;
            rawContent = ContentExtractor.StripFrontmatter(file.Content);
        }
        else
        {
            defaultFront = heading;
            var section = ContentExtractor.ExtractSection(file.Content, heading);
            if (section is null) return Results.NotFound();
            rawContent = section;
        }

        var (markers, cleanedContent) = ContentExtractor.ParseMarkers(rawContent);
        var fronts = markers.Count > 0 ? markers : new List<string> { defaultFront };

        return Results.Ok(new ExtractedContentDto(fronts, cleanedContent));
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

        // Validate fileId if provided
        if (request.FileId.HasValue)
        {
            var fileExists = await db.MarkdownFiles
                .AnyAsync(f => f.Id == request.FileId.Value && f.UserId == user.Id);
            if (!fileExists)
                return Results.BadRequest(new { error = "validation_error", message = "File not found or does not belong to you" });
        }

        // Validate deckId if provided
        if (request.DeckId.HasValue)
        {
            var deckExists = await db.Decks
                .AnyAsync(d => d.Id == request.DeckId.Value && d.UserId == user.Id);
            if (!deckExists)
                return Results.BadRequest(new { error = "validation_error", message = "Deck not found or does not belong to you" });
        }

        // Check for duplicates — cards with same front text for same file (or same user if no file)
        var fronts = request.Cards.Select(c => c.Front.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingFronts = request.FileId.HasValue
            ? await db.Cards
                .Where(c => c.UserId == user.Id && c.FileId == request.FileId.Value && fronts.Contains(c.Front))
                .Select(c => c.Front)
                .ToListAsync()
            : await db.Cards
                .Where(c => c.UserId == user.Id && c.FileId == null && fronts.Contains(c.Front))
                .Select(c => c.Front)
                .ToListAsync();

        var existingFrontSet = existingFronts.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<Card>();
        var skipped = new List<SkippedCardDto>();

        foreach (var item in request.Cards)
        {
            var trimmedFront = item.Front.Trim();
            if (existingFrontSet.Contains(trimmedFront))
            {
                skipped.Add(new SkippedCardDto(trimmedFront, "Card with same front text already exists"));
                continue;
            }

            // Also skip duplicates within the same batch
            if (created.Any(c => c.Front.Equals(trimmedFront, StringComparison.OrdinalIgnoreCase)))
            {
                skipped.Add(new SkippedCardDto(trimmedFront, "Duplicate within batch"));
                continue;
            }

            var cardType = request.FileId.HasValue
                ? (item.SourceHeading is not null ? "section" : "file")
                : "custom";

            var card = new Card
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FileId = request.FileId,
                SourceHeading = item.SourceHeading,
                Front = trimmedFront,
                Back = item.Back.Trim(),
                CardType = cardType,
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
            c.Id, c.FileId, c.SourceHeading, c.Front, c.Back,
            c.CardType, c.State, c.CreatedAt,
            request.DeckId.HasValue
                ? [new CardDeckInfoDto(request.DeckId.Value, "")]
                : [])).ToList();

        return Results.Created("/api/cards/bulk", new BulkCreateCardsResponse(createdDtos, skipped));
    }

    private static CardDto ToDto(Card c) =>
        new(c.Id, c.FileId, c.SourceHeading, c.Front, c.Back, c.CardType, c.State, c.CreatedAt,
            c.DeckCards.Select(dc => new CardDeckInfoDto(dc.DeckId, dc.Deck.Name)).ToList());
}
