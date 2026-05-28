namespace Fasolt.Server.Api.McpResources;

internal static class ResourceUris
{
    public const string DeckPrefix = "fasolt://deck/";
    public const string DueToday = "fasolt://due-today";
    public const string Recent = "fasolt://recent";
    public const string DeckTemplate = "fasolt://deck/{deckId}";

    public static bool TryParseDeck(string uri, out string deckId)
    {
        if (uri.StartsWith(DeckPrefix, StringComparison.Ordinal))
        {
            deckId = uri[DeckPrefix.Length..];
            return deckId.Length > 0;
        }
        deckId = string.Empty;
        return false;
    }
}
