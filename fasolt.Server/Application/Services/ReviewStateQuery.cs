using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

/// <summary>
/// Helpers for the per-user SRS state that lives in <see cref="ReviewState"/>.
/// A card with no row for the user is "new": not suspended, no due date,
/// no FSRS parameters. Rows are created lazily on first review or suspend.
/// </summary>
public static class ReviewStateQuery
{
    /// <summary>Cards the user has not suspended (no row ⇒ not suspended).</summary>
    public static Expression<Func<Card, bool>> NotSuspendedBy(string userId) =>
        c => !c.ReviewStates.Any(r => r.UserId == userId && r.IsSuspended);

    /// <summary>
    /// Cards due at or before <paramref name="now"/>. No row, or a row with a null
    /// due date, both mean "due now" — matching the old <c>DueAt == null</c> semantics.
    /// </summary>
    public static Expression<Func<Card, bool>> DueBy(string userId, DateTimeOffset now) =>
        c => !c.ReviewStates.Any(r => r.UserId == userId && r.DueAt > now);

    /// <summary>
    /// Loads the user's state for a card, creating it first if absent. The returned
    /// entity is tracked, so callers mutate it and call <c>SaveChangesAsync</c> as usual.
    /// </summary>
    public static async Task<ReviewState> GetOrCreateAsync(AppDbContext db, string userId, Guid cardId)
    {
        var state = await db.ReviewStates
            .FirstOrDefaultAsync(r => r.UserId == userId && r.CardId == cardId);

        if (state is not null) return state;

        await EnsureExistAsync(db, userId, [cardId]);

        return await db.ReviewStates
            .FirstAsync(r => r.UserId == userId && r.CardId == cardId);
    }

    /// <summary>
    /// Creates default ("new") rows for the given cards, skipping the ones that already
    /// have one. Done as a single INSERT ... ON CONFLICT DO NOTHING so that two requests
    /// touching the same card for the first time cannot collide on the (UserId, CardId)
    /// primary key — the loser simply keeps the winner's row and updates it.
    /// The rows are written immediately, outside <c>SaveChangesAsync</c>; a row that is
    /// never updated afterwards carries no information beyond "new" anyway.
    /// </summary>
    public static async Task EnsureExistAsync(AppDbContext db, string userId, IReadOnlyCollection<Guid> cardIds)
    {
        if (cardIds.Count == 0) return;

        var ids = cardIds as Guid[] ?? [.. cardIds];

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ReviewStates" ("UserId", "CardId", "State", "IsSuspended")
            SELECT {userId}, id, 'new', false FROM unnest({ids}::uuid[]) AS id
            ON CONFLICT ("UserId", "CardId") DO NOTHING
            """);
    }

    /// <summary>Loads the user's states for a set of cards, keyed by card id.</summary>
    public static async Task<Dictionary<Guid, ReviewState>> LoadForCardsAsync(
        AppDbContext db, string userId, IReadOnlyCollection<Guid> cardIds)
    {
        if (cardIds.Count == 0) return [];

        return await db.ReviewStates
            .Where(r => r.UserId == userId && cardIds.Contains(r.CardId))
            .ToDictionaryAsync(r => r.CardId);
    }
}
