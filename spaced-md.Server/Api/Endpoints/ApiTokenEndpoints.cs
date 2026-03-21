using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Api.Auth;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Domain.Entities;
using SpacedMd.Server.Infrastructure.Data;

namespace SpacedMd.Server.Api.Endpoints;

public static class ApiTokenEndpoints
{
    public static void MapApiTokenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tokens").RequireAuthorization();

        group.MapPost("/", Create);
        group.MapGet("/", List);
        group.MapDelete("/{id:guid}", Revoke);
    }

    private static async Task<IResult> Create(
        CreateTokenRequest request,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "validation_error", message = "Token name is required" });

        if (request.Name.Length > 100)
            return Results.BadRequest(new { error = "validation_error", message = "Token name must be 100 characters or less" });

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return Results.BadRequest(new { error = "validation_error", message = "Expiration date must be in the future" });

        // Generate token: sm_ + 32 random bytes as hex (64 hex chars)
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = "sm_" + Convert.ToHexStringLower(rawBytes);
        var hash = BearerTokenHandler.ComputeHash(rawToken);
        var prefix = rawToken[..8]; // "sm_XXXXX"

        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            TokenHash = hash,
            TokenPrefix = prefix,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = request.ExpiresAt,
        };

        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();

        return Results.Created($"/api/tokens/{token.Id}", new CreateTokenResponse(
            token.Id,
            token.Name,
            rawToken,
            token.CreatedAt,
            token.ExpiresAt));
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTimeOffset.UtcNow;

        var tokens = await db.ApiTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TokenListItemDto(
                t.Id,
                t.Name,
                t.TokenPrefix,
                t.CreatedAt,
                t.LastUsedAt,
                t.ExpiresAt,
                t.ExpiresAt.HasValue && t.ExpiresAt.Value < now,
                t.RevokedAt.HasValue))
            .ToListAsync();

        return Results.Ok(tokens);
    }

    private static async Task<IResult> Revoke(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var token = await db.ApiTokens
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (token is null)
            return Results.NotFound(new { error = "not_found", message = "Token not found" });

        if (token.RevokedAt.HasValue)
            return Results.Ok(new { message = "Token already revoked" });

        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Token revoked" });
    }
}
