using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Middleware;

/// <summary>
/// Gives the anonymous library routes real metadata. The SPA can't do this on its
/// own — crawlers and link unfurlers never run the client bundle — so for
/// <c>/library</c> and <c>/library/{publicId}</c> we serve the built index.html
/// with the title, description and OG tags rewritten server-side. Also serves a
/// generated sitemap that includes every public deck.
/// </summary>
/// <remarks>
/// Must be registered before UseStaticFiles: a static wwwroot/sitemap.xml would
/// otherwise shadow the generated one. When index.html is absent (dev, where Vite
/// serves the SPA) every HTML request falls straight through.
/// </remarks>
public partial class SeoMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    private const string SitemapPath = "/sitemap.xml";
    private const string LibraryPath = "/library";

    private const string DefaultLibraryTitle = "Deck library — fasolt";
    private const string DefaultLibraryDescription =
        "Browse public flashcard decks shared by the fasolt community and import them into your own account.";

    /// <summary>Static routes that belong in the sitemap alongside the public decks.</summary>
    private static readonly string[] StaticRoutes = ["/", "/algorithm", "/privacy", "/terms", "/impressum", "/library"];

    [GeneratedRegex(@"<title>.*?</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();

    [GeneratedRegex("""<meta\s+name="description"[^>]*/?>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DescriptionTag();

    private string? _cachedIndexHtml;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path;

        if (path.Equals(SitemapPath, StringComparison.OrdinalIgnoreCase))
        {
            await WriteSitemap(context, context.RequestServices.GetRequiredService<AppDbContext>());
            return;
        }

        if (!IsLibraryRoute(path, out var deckPublicId) || !WantsHtml(context.Request))
        {
            await next(context);
            return;
        }

        var indexHtml = await ReadIndexHtml();
        if (indexHtml is null)
        {
            await next(context);
            return;
        }

        string title = DefaultLibraryTitle;
        string description = DefaultLibraryDescription;
        var noIndex = false;

        if (deckPublicId is not null)
        {
            var libraryService = context.RequestServices.GetRequiredService<LibraryService>();
            var deck = await libraryService.GetPublicDeck(deckPublicId);
            if (deck is null)
            {
                // Unknown or private deck: let the SPA render its own not-found state
                // rather than advertising a deck page that doesn't exist.
                await next(context);
                return;
            }

            title = deck.AuthorHandle is null
                ? $"{deck.Name} — fasolt"
                : $"{deck.Name} by @{deck.AuthorHandle} — fasolt";
            description = BuildDeckDescription(deck.Description, deck.CardCount, deck.AuthorHandle);

            // Unlisted means "reachable by link", not "findable" — keep it out of
            // search indexes while still giving the link a decent unfurl.
            noIndex = deck.Visibility != "public";
        }

        var canonicalUrl = BuildCanonicalUrl(context, path);
        var html = Inject(indexHtml, title, description, canonicalUrl, noIndex);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html, Encoding.UTF8);
    }

    private static bool IsLibraryRoute(PathString path, out string? deckPublicId)
    {
        deckPublicId = null;

        var value = path.Value?.TrimEnd('/');
        if (string.IsNullOrEmpty(value)) return false;

        if (value.Equals(LibraryPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // /library/{publicId} — one segment only; anything deeper is not a deck page.
        const string prefix = LibraryPath + "/";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var rest = value[prefix.Length..];
        if (rest.Length == 0 || rest.Contains('/')) return false;

        deckPublicId = Uri.UnescapeDataString(rest);
        return true;
    }

    private static bool WantsHtml(HttpRequest request)
    {
        // Browsers send text/html; plenty of crawlers and link unfurlers send only
        // */* or nothing at all, and they are exactly the audience for these tags.
        var accept = request.Headers.Accept.ToString();
        return string.IsNullOrEmpty(accept)
            || accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("*/*", StringComparison.Ordinal);
    }

    private async Task<string?> ReadIndexHtml()
    {
        if (_cachedIndexHtml is not null) return _cachedIndexHtml;

        var file = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!file.Exists) return null;

        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        // The built index.html is immutable for the lifetime of the process.
        return _cachedIndexHtml = await reader.ReadToEndAsync();
    }

    private static string BuildDeckDescription(string? deckDescription, int cardCount, string? authorHandle)
    {
        var trimmed = deckDescription?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            return trimmed.Length > 300 ? trimmed[..297] + "…" : trimmed;

        var cards = $"{cardCount} flashcard{(cardCount == 1 ? "" : "s")}";
        return authorHandle is null
            ? $"A shared deck of {cards} on fasolt."
            : $"A shared deck of {cards} by @{authorHandle} on fasolt.";
    }

    private static string BuildCanonicalUrl(HttpContext context, PathString path) =>
        $"{context.Request.Scheme}://{context.Request.Host}{path.Value}";

    private static string Inject(string html, string title, string description, string canonicalUrl, bool noIndex)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeDescription = WebUtility.HtmlEncode(description);
        var safeUrl = WebUtility.HtmlEncode(canonicalUrl);

        html = TitleTag().Replace(html, $"<title>{safeTitle}</title>", 1);
        html = DescriptionTag().Replace(html, $"""<meta name="description" content="{safeDescription}" />""", 1);

        var tags = $"""
            <link rel="canonical" href="{safeUrl}" />
            <meta property="og:type" content="website" />
            <meta property="og:site_name" content="fasolt" />
            <meta property="og:title" content="{safeTitle}" />
            <meta property="og:description" content="{safeDescription}" />
            <meta property="og:url" content="{safeUrl}" />
            <meta name="twitter:card" content="summary" />
            <meta name="twitter:title" content="{safeTitle}" />
            <meta name="twitter:description" content="{safeDescription}" />
            """;

        if (noIndex)
            tags += "\n<meta name=\"robots\" content=\"noindex\" />";

        var headClose = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headClose < 0 ? html : html.Insert(headClose, tags + "\n  ");
    }

    private static async Task WriteSitemap(HttpContext context, AppDbContext db)
    {
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

        var decks = await db.Decks
            .Where(d => d.Visibility == DeckVisibility.Public)
            .OrderByDescending(d => d.PublishedAt)
            .Take(5000)
            .Select(d => new { d.PublicId, d.PublishedAt })
            .ToListAsync();

        var xml = new StringBuilder();
        xml.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        xml.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        foreach (var route in StaticRoutes)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{WebUtility.HtmlEncode(baseUrl + route)}</loc>");
            xml.AppendLine("  </url>");
        }

        foreach (var deck in decks)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{WebUtility.HtmlEncode($"{baseUrl}/library/{deck.PublicId}")}</loc>");
            if (deck.PublishedAt is { } publishedAt)
                xml.AppendLine($"    <lastmod>{publishedAt.UtcDateTime:yyyy-MM-dd}</lastmod>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        context.Response.ContentType = "application/xml; charset=utf-8";
        // Anonymous and unauthenticated: let caches absorb crawler traffic so each
        // hit does not turn into a deck query.
        context.Response.Headers.CacheControl = "public, max-age=3600";
        await context.Response.WriteAsync(xml.ToString(), Encoding.UTF8);
    }
}
