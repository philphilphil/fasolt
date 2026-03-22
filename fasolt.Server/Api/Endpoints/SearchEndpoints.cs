using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/search").RequireAuthorization();
        group.MapGet("/", Search);
    }

    private static async Task<IResult> Search(
        string? q,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SearchService searchService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await searchService.Search(user.Id, q);
        return Results.Ok(result);
    }
}

public record SearchResponse(
    List<CardSearchResult> Cards,
    List<DeckSearchResult> Decks);

public record CardSearchResult(Guid Id, string Headline, string State);
public record DeckSearchResult(Guid Id, string Headline, int CardCount);
