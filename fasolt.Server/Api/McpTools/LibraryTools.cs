using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

/// <summary>
/// The deck-sharing surface: browsing the public library, importing a shared deck
/// into the caller's account, and publishing one of the caller's own decks. Mirrors
/// <c>/api/library</c> and <c>PUT /api/decks/{id}/visibility</c>, including which
/// conditions are errors and which are idempotent successes.
/// </summary>
[McpServerToolType]
public class LibraryTools(
    LibraryService libraryService,
    DeckSubscriptionService subscriptionService,
    PublishingService publishingService,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Browse or search the public deck library — decks other Fasolt users have published. Returns items (id, name, description, authorHandle, cardCount, copyCount, publishedAt) plus totalCount, page and pageSize for paging. Pass an item's id to import_deck to add it to the user's account.")]
    public async Task<string> ListPublicDecks(
        [Description("Optional full-text search over deck names and descriptions")] string? query = null,
        [Description("Sort order: 'popular' (default — most imported) or 'recent' (most recently published)")] string? sort = null,
        [Description("1-based page number (default 1)")] int? page = null,
        [Description("Results per page (1-50, default 24)")] int? pageSize = null)
    {
        // The library is world-readable, so unlike every other tool here this one
        // needs no user id — the /mcp endpoint's own auth is the only gate.
        var result = await libraryService.ListPublicDecks(
            query, sort, page ?? 1, pageSize ?? LibraryService.DefaultPageSize);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Import a shared deck into the user's account. Mode 'copy' clones the cards into a deck the user owns outright and can edit; mode 'link' subscribes to the author's deck, which stays in sync with the author's changes but is read-only. Linking a deck that is already linked succeeds without creating a second copy (alreadyLinked: true).")]
    public async Task<string> ImportDeck(
        [Description("Public id of the shared deck — the `id` field from list_public_decks, or the last path segment of a /library/... share link")] string publicId,
        [Description("'copy' for an editable clone, 'link' for a live read-only subscription")] string mode)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        var normalized = mode?.Trim().ToLowerInvariant();
        if (normalized is not "copy" and not "link")
            return McpErrors.Structured("invalid_mode",
                "Mode must be 'copy' (editable clone owned by the user) or 'link' (live read-only subscription).");

        return normalized == "copy"
            ? await Copy(userId, publicId)
            : await Link(userId, publicId);
    }

    private async Task<string> Copy(string userId, string publicId)
    {
        var result = await libraryService.CopyDeck(userId, publicId);

        return result.Error switch
        {
            CopyDeckError.NotFound => DeckNotShared(),
            CopyDeckError.DeckTooLarge => McpErrors.Structured("deck_too_large",
                $"Decks with more than {PublishingService.MaxCardsInPublicDeck} cards cannot be imported."),
            _ => JsonSerializer.Serialize(new
            {
                mode = "copy",
                deck = result.Deck,
                deckUrl = DeckUrl(result.Deck!.Id),
            }, McpJson.Options),
        };
    }

    private async Task<string> Link(string userId, string publicId)
    {
        var result = await subscriptionService.Subscribe(userId, publicId);

        return result.Error switch
        {
            SubscribeError.NotFound => DeckNotShared(),
            SubscribeError.OwnDeck => McpErrors.Structured("own_deck",
                "This deck already belongs to the user — a deck cannot be linked to its own author. "
                + "It is already in list_decks."),
            // Subscribing twice is not an error, but the agent must not report a
            // fresh import that never happened.
            _ => JsonSerializer.Serialize(new
            {
                mode = "link",
                alreadyLinked = !result.Created,
                deck = result.Deck,
                deckUrl = DeckUrl(result.Deck!.Id),
            }, McpJson.Options),
        };
    }

    /// <summary>
    /// A deck that is private, deleted or never existed is indistinguishable from
    /// outside, and must stay that way — the message covers all three.
    /// </summary>
    private static string DeckNotShared() => McpErrors.Structured("deck_not_found",
        "No shared deck with that id. It may never have existed, or the author may have made it private again.");

    [McpServerTool, Description("Publish or unpublish one of the user's own decks. 'public' lists it in the public library, 'unlisted' makes it reachable only through its share link, 'private' takes it down and removes everyone who had linked it. Returns the deck and its shareUrl. Publishing requires an account handle, which the user claims once in the Fasolt web app settings.")]
    public async Task<string> PublishDeck(
        [Description("ID of one of the user's own decks (from list_decks)")] string deckId,
        [Description("'private', 'unlisted', or 'public'")] string visibility)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        if (!DeckVisibilityWire.TryParse(visibility, out var parsed))
            return McpErrors.Structured("invalid_visibility",
                "Visibility must be one of: private, unlisted, public.");

        var result = await publishingService.SetVisibility(userId, deckId, parsed);

        return result.Error switch
        {
            SetVisibilityError.DeckNotFound => McpErrors.Structured("deck_not_found",
                "No deck with that id in the user's account."),
            SetVisibilityError.HandleRequired => McpErrors.Structured("handle_required",
                "Publishing needs an account handle, and this account has none yet. Handles are claimed "
                + "in the Fasolt web app under Settings — ask the user to claim one there, then retry."),
            SetVisibilityError.PublishingDisabled => McpErrors.Structured("publishing_disabled",
                "Publishing is disabled for this account. Contact support if this is unexpected."),
            SetVisibilityError.DeckTooLarge => McpErrors.Structured("deck_too_large",
                $"Published decks are limited to {PublishingService.MaxCardsInPublicDeck} cards. "
                + "Split the deck before publishing it."),
            SetVisibilityError.PublicDeckLimit => McpErrors.Structured("public_deck_limit",
                $"This account already has the maximum of {PublishingService.MaxPublicDecksPerUser} public decks. "
                + "Set one of them back to private first."),
            _ => JsonSerializer.Serialize(new
            {
                deck = result.Deck,
                // A private deck has no link worth handing back; the null is dropped
                // by McpJson's ignore-nulls policy.
                shareUrl = parsed == DeckVisibility.Private ? null : ShareUrl(result.Deck!.Id),
            }, McpJson.Options),
        };
    }

    /// <summary>
    /// Derived from the request rather than configured, exactly like the deck deep
    /// link in <see cref="CardTools"/>, so self-hosted installs get their own host.
    /// </summary>
    private string BaseUrl()
    {
        var request = httpContextAccessor.HttpContext!.Request;
        return $"{request.Scheme}://{request.Host}";
    }

    private string ShareUrl(string deckPublicId) => $"{BaseUrl()}/library/{deckPublicId}";

    private string DeckUrl(string deckPublicId) => $"{BaseUrl()}/decks/{deckPublicId}";
}
