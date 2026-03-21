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

    private static CardDto ToDto(Card c) =>
        new(c.Id, c.FileId, c.SourceHeading, c.Front, c.Back, c.CardType, c.State, c.CreatedAt,
            c.DeckCards.Select(dc => new CardDeckInfoDto(dc.DeckId, dc.Deck.Name)).ToList());
}
