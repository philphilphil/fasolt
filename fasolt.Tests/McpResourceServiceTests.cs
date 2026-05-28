using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fasolt.Tests;

public class McpResourceServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private McpResourceService CreateService(AppDbContext db) =>
        new McpResourceService(
            db,
            new ReviewService(db, TimeProvider.System, new StudyStatsService(db, TimeProvider.System)),
            TimeProvider.System);

    [Fact]
    public async Task ListUserResourcesAsync_NoDecks_ReturnsTwoStatics()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var entries = await svc.ListUserResourcesAsync(UserId);

        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Uri == "fasolt://due-today");
        entries.Should().Contain(e => e.Uri == "fasolt://recent");
    }

    [Fact]
    public async Task RenderDeckAsync_BasicCard_FormatsFrontBackAndHeader()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "German Verbs", "Common verbs");
        var card = await cardSvc.CreateCard(UserId, "essen", "to eat", null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().NotBeNull();
        md.Should().Contain("# Deck: German Verbs");
        md.Should().Contain("1 card");
        md.Should().Contain("Common verbs");
        md.Should().Contain("**Front:** essen");
        md.Should().Contain("**Back:** to eat");
    }

    [Fact]
    public async Task RenderDeckAsync_EmptyDeck_RendersHeaderAndNoCardsLine()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var deck = await deckSvc.CreateDeck(UserId, "Empty Deck", null);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().Contain("# Deck: Empty Deck");
        md.Should().Contain("0 cards");
        md.Should().Contain("No cards.");
    }

    [Fact]
    public async Task RenderDeckAsync_UnknownDeck_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var md = await svc.RenderDeckAsync(UserId, "does-not-exist");

        md.Should().BeNull();
    }

    [Fact]
    public async Task RenderDeckAsync_OtherUsersDeck_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var deck = await deckSvc.CreateDeck(UserId, "Mine", null);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync("different-user-id", deck.Id);

        md.Should().BeNull();
    }

    [Fact]
    public async Task RenderDeckAsync_NoDescription_OmitsDescriptionLine()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var deck = await deckSvc.CreateDeck(UserId, "NoDesc", null);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().NotBeNull();
        md.Should().Contain("# Deck: NoDesc");
        // Header line followed directly by a card section / no cards, not by an empty description
        md.Should().NotContain("\n\n\n\n");
    }

    [Fact]
    public async Task RenderDeckAsync_SuspendedCard_ExcludedFromOutput()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Mixed", null);
        var kept = await cardSvc.CreateCard(UserId, "kept-front", "kept-back", null);
        var suspended = await cardSvc.CreateCard(UserId, "suspended-front", "suspended-back", null);

        await deckSvc.AddCards(UserId, deck.Id, [kept.Id, suspended.Id]);

        // Suspend the second card via direct DB mutation (no service method needed for the test)
        var card = await db.Cards.FirstAsync(c => c.PublicId == suspended.Id);
        card.IsSuspended = true;
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().Contain("kept-front");
        md.Should().NotContain("suspended-front");
    }

    [Fact]
    public async Task RenderDeckAsync_CardWithSource_IncludesSourceFooter()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Sourced", null);
        var card = await cardSvc.CreateCard(UserId, "front", "back", "notes/german/verbs.md");
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().Contain("*Source: notes/german/verbs.md*");
    }

    [Fact]
    public async Task RenderDeckAsync_CardWithSvg_AppendsSvgNote()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Svg", null);
        var card = await cardSvc.CreateCard(
            UserId, "front", "back", null,
            frontSvg: "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><circle cx=\"50\" cy=\"50\" r=\"40\"/></svg>");
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().Contain("[has SVG image — use get_card for full content]");
        md.Should().NotContain("<svg");
    }

    [Fact]
    public async Task RenderDeckAsync_EmptyBack_OmitsBackBlock()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "OneSided", null);

        // Insert directly with empty back — CardService validation likely rejects empty back
        var cardEntity = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..12],
            UserId = UserId,
            Front = "question-only",
            Back = "",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Cards.Add(cardEntity);
        await db.SaveChangesAsync();
        await deckSvc.AddCards(UserId, deck.Id, [cardEntity.PublicId]);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().Contain("**Front:** question-only");
        md.Should().NotContain("**Back:**");
    }

    [Fact]
    public async Task RenderDeckAsync_OverSoftCap_TruncatesWithFooter()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var cardSvc = new CardService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Big Deck", null);

        // 105 cards — over the 100-card soft cap
        var cardIds = new List<string>();
        for (var i = 0; i < 105; i++)
        {
            var card = await cardSvc.CreateCard(UserId, $"front-{i:000}", $"back-{i:000}", null);
            cardIds.Add(card.Id);
        }
        await deckSvc.AddCards(UserId, deck.Id, cardIds);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().NotBeNull();
        md.Should().Contain("front-000"); // first card shown
        md.Should().Contain("Showing 100 of 105 cards");
        md.Should().NotContain("front-100"); // truncated
    }

    [Fact]
    public async Task RenderDeckAsync_OverSizeBudget_TruncatesWithFooter()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var cardSvc = new CardService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Heavy Deck", null);

        // 50 cards, ~2 KB each → ~100 KB total, exceeds 80 KB budget
        var bigText = new string('x', 2000);
        var cardIds = new List<string>();
        for (var i = 0; i < 50; i++)
        {
            var card = await cardSvc.CreateCard(UserId, $"front-{i:00}-{bigText}", $"back-{i:00}", null);
            cardIds.Add(card.Id);
        }
        await deckSvc.AddCards(UserId, deck.Id, cardIds);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().NotBeNull();
        md!.Length.Should().BeLessThan(100 * 1024); // truncated under 100 KB
        md.Should().Contain("Showing"); // truncation footer present
        md.Should().Contain("of 50 cards");
    }

    [Fact]
    public async Task RenderDueTodayAsync_NoDueCards_RendersEmptyMessage()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var md = await svc.RenderDueTodayAsync(UserId);

        md.Should().Contain("# Due Today");
        md.Should().Contain("No cards.");
    }

    [Fact]
    public async Task RenderDueTodayAsync_GroupsByDeck()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var cardSvc = new CardService(db);

        var german = await deckSvc.CreateDeck(UserId, "German Verbs", null);
        var french = await deckSvc.CreateDeck(UserId, "French Vocab", null);

        var ger1 = await cardSvc.CreateCard(UserId, "essen", "to eat", null);
        var fr1 = await cardSvc.CreateCard(UserId, "manger", "to eat", null);
        await deckSvc.AddCards(UserId, german.Id, [ger1.Id]);
        await deckSvc.AddCards(UserId, french.Id, [fr1.Id]);

        var svc = CreateService(db);
        var md = await svc.RenderDueTodayAsync(UserId);

        md.Should().Contain("## French Vocab"); // alphabetical
        md.Should().Contain("## German Verbs");
        md.Should().Contain("**Front:** essen");
        md.Should().Contain("**Front:** manger");

        // French should appear before German (alphabetical)
        var fIdx = md.IndexOf("## French Vocab");
        var gIdx = md.IndexOf("## German Verbs");
        fIdx.Should().BeLessThan(gIdx);
    }

    [Fact]
    public async Task RenderDueTodayAsync_CardWithNoDeck_GoesInNoDeckBucket()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        await cardSvc.CreateCard(UserId, "orphan-front", "orphan-back", null);

        var svc = CreateService(db);
        var md = await svc.RenderDueTodayAsync(UserId);

        md.Should().Contain("## (no deck)");
        md.Should().Contain("**Front:** orphan-front");
    }

    [Fact]
    public async Task RenderDueTodayAsync_SuspendedCard_Excluded()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);

        var kept = await cardSvc.CreateCard(UserId, "kept", "ok", null);
        var sus = await cardSvc.CreateCard(UserId, "suspended", "no", null);
        var susEntity = await db.Cards.FirstAsync(c => c.PublicId == sus.Id);
        susEntity.IsSuspended = true;
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var md = await svc.RenderDueTodayAsync(UserId);

        md.Should().Contain("kept");
        md.Should().NotContain("suspended");
    }

    [Fact]
    public async Task RenderDueTodayAsync_FirstCardOfGroupTruncated_DoesNotOrphanHeader()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var cardSvc = new CardService(db);

        // Two decks. Deck A fills ~78 KB (75 cards × ~1044 chars each) — just under the 80 KB budget.
        // Deck Z gets 1 card with a ~5 KB front that pushes past the budget, so its header
        // must NOT appear in the output (the card is truncated before the header is written).
        var deckA = await deckSvc.CreateDeck(UserId, "AlphaDeck", null);
        var deckZ = await deckSvc.CreateDeck(UserId, "ZetaDeck", null);

        var aIds = new List<string>();
        for (var i = 0; i < 75; i++)
        {
            var c = await cardSvc.CreateCard(UserId, $"a-front-{i:00}-{new string('a', 1000)}", "back", null);
            aIds.Add(c.Id);
        }
        await deckSvc.AddCards(UserId, deckA.Id, aIds);

        // 5 KB front — well within the 10 KB card limit but tips the total past 80 KB
        var zCard = await cardSvc.CreateCard(UserId, new string('z', 5000), "back", null);
        await deckSvc.AddCards(UserId, deckZ.Id, [zCard.Id]);

        var svc = CreateService(db);
        var md = await svc.RenderDueTodayAsync(UserId);

        md.Should().NotBeNull();
        md.Should().Contain("## AlphaDeck");
        // ZetaDeck's header must NOT appear because its only card was truncated
        md.Should().NotContain("## ZetaDeck");
    }
}
