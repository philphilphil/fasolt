using System.Security.Claims;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Api.Endpoints;

public static class SnapshotEndpoints
{
    public static void MapSnapshotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization("EmailVerified").RequireRateLimiting("api");

        group.MapPost("/snapshots", CreateAll);
        group.MapGet("/snapshots/recent", ListRecent);
        group.MapGet("/decks/{deckId}/snapshots", ListByDeck);
        group.MapGet("/snapshots/{id}", GetById);
        group.MapGet("/snapshots/{id}/diff", GetDiff);
        group.MapPost("/snapshots/{id}/restore", Restore);
        group.MapDelete("/snapshots/{id}", Delete);
    }

    private static async Task<IResult> CreateAll(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await snapshotService.CreateAll(user.Id);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListRecent(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var list = await snapshotService.ListRecent(user.Id);
        return Results.Ok(list);
    }

    private static async Task<IResult> ListByDeck(
        string deckId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var list = await snapshotService.ListByDeck(user.Id, deckId);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetById(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var snapshot = await snapshotService.GetById(user.Id, id);
        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    private static async Task<IResult> GetDiff(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var diff = await snapshotService.ComputeDiff(user.Id, id);
        return diff is null ? Results.NotFound() : Results.Ok(diff);
    }

    private static async Task<IResult> Restore(
        string id,
        RestoreRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var success = await snapshotService.Restore(user.Id, id, request);
        return success ? Results.Ok() : Results.NotFound();
    }

    private static async Task<IResult> Delete(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var success = await snapshotService.Delete(user.Id, id);
        return success ? Results.NoContent() : Results.NotFound();
    }
}
