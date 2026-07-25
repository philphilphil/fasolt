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

    /// <summary>Loads the user's state for a card, creating (and tracking) it if absent.</summary>
    public static async Task<ReviewState> GetOrCreateAsync(AppDbContext db, string userId, Guid cardId)
    {
        var state = await db.ReviewStates
            .FirstOrDefaultAsync(r => r.UserId == userId && r.CardId == cardId);

        if (state is null)
        {
            state = new ReviewState { UserId = userId, CardId = cardId };
            db.ReviewStates.Add(state);
        }

        return state;
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
