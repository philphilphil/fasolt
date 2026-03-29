namespace Fasolt.Server.Api.Helpers;

public static class UrlHelpers
{
    public static bool IsLocalUrl(string url) =>
        !string.IsNullOrEmpty(url) &&
        url.StartsWith('/') &&
        !url.StartsWith("//") &&
        !url.StartsWith("/\\");
}
