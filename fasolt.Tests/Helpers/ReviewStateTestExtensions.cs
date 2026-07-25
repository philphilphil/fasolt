using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Helpers;

public static class ReviewStateTestExtensions
{
    /// <summary>
    /// Returns the user's tracked <see cref="ReviewState"/> for a card, creating it if
    /// absent. The caller mutates it and calls SaveChangesAsync — the test equivalent of
    /// the lazy creation the services do.
    /// </summary>
    public static async Task<ReviewState> ReviewStateFor(this AppDbContext db, string userId, Guid cardId)
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

    /// <summary>Same, but resolves the card by its public id.</summary>
    public static async Task<ReviewState> ReviewStateForPublicId(this AppDbContext db, string userId, string cardPublicId)
    {
        var cardId = await db.Cards.Where(c => c.PublicId == cardPublicId).Select(c => c.Id).FirstAsync();
        return await db.ReviewStateFor(userId, cardId);
    }
}
