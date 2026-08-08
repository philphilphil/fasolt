using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Helpers;

/// <summary>
/// Seeding helpers for the link-mode tests: an author account, a shared deck with
/// cards, and the subscription that links the two.
/// </summary>
internal static class LinkedDeckTestData
{
    public static async Task<string> AddUser(AppDbContext db, string? handle = null)
    {
        var id = Guid.NewGuid().ToString();
        var email = $"{id}@fasolt.test";
        db.Users.Add(new AppUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            Handle = handle,
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Deck> AddDeck(
        AppDbContext db,
        string userId,
        DeckVisibility visibility = DeckVisibility.Public,
        string name = "Shared Deck",
        int cardCount = 0,
        string sourceFilePrefix = "vault/author-notes")
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = visibility,
            PublishedAt = visibility == DeckVisibility.Private ? null : DateTimeOffset.UtcNow,
        };
        db.Decks.Add(deck);

        for (var i = 0; i < cardCount; i++)
            AddCard(db, deck, $"{name} Q{i}", $"{name} A{i}", $"{sourceFilePrefix}-{i}.md");

        await db.SaveChangesAsync();
        return deck;
    }

    public static Card AddCard(AppDbContext db, Deck deck, string front, string back, string? sourceFile = null)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = deck.UserId,
            SourceFile = sourceFile,
            Front = front,
            Back = back,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Cards.Add(card);
        db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
        return card;
    }

    public static async Task<DeckSubscription> Subscribe(AppDbContext db, string userId, Deck deck)
    {
        var subscription = new DeckSubscription
        {
            UserId = userId,
            DeckId = deck.Id,
            SubscribedAt = DateTimeOffset.UtcNow,
        };
        db.DeckSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription;
    }
}
