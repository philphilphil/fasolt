namespace Fasolt.Server.Domain.Entities;

/// <summary>
/// Who can see a deck. Stored as a string so the column stays readable and
/// re-orderable. Anything other than <see cref="Private"/> is reachable by
/// anonymous visitors through the library API.
/// </summary>
public enum DeckVisibility
{
    /// <summary>Owner only. The default for every deck.</summary>
    Private,

    /// <summary>Anyone with the direct link can view and import; not listed.</summary>
    Unlisted,

    /// <summary>Listed in the public library.</summary>
    Public,
}

/// <summary>
/// Maps <see cref="DeckVisibility"/> to and from its lowercase wire form, so the
/// JSON contract stays camelCase-consistent with the rest of the API rather than
/// leaking enum ordinals or PascalCase names.
/// </summary>
public static class DeckVisibilityWire
{
    public static string ToWire(this DeckVisibility visibility) => visibility switch
    {
        DeckVisibility.Public => "public",
        DeckVisibility.Unlisted => "unlisted",
        _ => "private",
    };

    public static bool TryParse(string? value, out DeckVisibility visibility)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "private": visibility = DeckVisibility.Private; return true;
            case "unlisted": visibility = DeckVisibility.Unlisted; return true;
            case "public": visibility = DeckVisibility.Public; return true;
            default: visibility = DeckVisibility.Private; return false;
        }
    }
}
