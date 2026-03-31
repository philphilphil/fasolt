using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class AccountDataService(AppDbContext db)
{
    public async Task<AccountExport> ExportUserData(AppUser user)
    {
        var userId = user.Id;

        var cards = await db.Cards
            .Where(c => c.UserId == userId)
            .Include(c => c.DeckCards)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var decks = await db.Decks
            .Where(d => d.UserId == userId)
            .Include(d => d.Cards)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        var snapshots = await db.DeckSnapshots
            .Where(s => s.UserId == userId)
            .Include(s => s.Deck)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        var consentGrants = await db.ConsentGrants
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var deviceToken = await db.DeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == userId);

        var cardPublicIdMap = cards.ToDictionary(c => c.Id, c => c.PublicId);

        return new AccountExport(
            ExportedAt: DateTimeOffset.UtcNow,
            Account: new AccountExportProfile(
                Email: user.Email!,
                EmailConfirmed: user.EmailConfirmed,
                ExternalProvider: user.ExternalProvider,
                DesiredRetention: user.DesiredRetention,
                MaximumInterval: user.MaximumInterval,
                NotificationIntervalHours: user.NotificationIntervalHours
            ),
            Decks: decks.Select(d => new AccountExportDeck(
                Name: d.Name,
                Description: d.Description,
                IsSuspended: d.IsSuspended,
                CreatedAt: d.CreatedAt,
                Cards: d.Cards.Select(dc => cardPublicIdMap.GetValueOrDefault(dc.CardId, "")).Where(id => id != "").ToList()
            )).ToList(),
            Cards: cards.Select(c => new AccountExportCard(
                PublicId: c.PublicId,
                Front: c.Front,
                Back: c.Back,
                FrontSvg: c.FrontSvg,
                BackSvg: c.BackSvg,
                SourceFile: c.SourceFile,
                SourceHeading: c.SourceHeading,
                State: c.State,
                Stability: c.Stability,
                Difficulty: c.Difficulty,
                Step: c.Step,
                DueAt: c.DueAt,
                LastReviewedAt: c.LastReviewedAt,
                IsSuspended: c.IsSuspended,
                CreatedAt: c.CreatedAt
            )).ToList(),
            Sources: cards.Where(c => c.SourceFile != null).Select(c => c.SourceFile!).Distinct().OrderBy(s => s).ToList(),
            Snapshots: snapshots.Select(s => new AccountExportSnapshot(
                DeckName: s.Deck?.Name,
                Version: s.Version,
                CardCount: s.CardCount,
                Data: s.Data,
                CreatedAt: s.CreatedAt
            )).ToList(),
            ConsentGrants: consentGrants.Select(c => new AccountExportConsentGrant(c.ClientId, c.GrantedAt)).ToList(),
            DeviceToken: deviceToken is null ? null : new AccountExportDeviceToken(deviceToken.Token, deviceToken.CreatedAt, deviceToken.UpdatedAt)
        );
    }
}
