namespace Fasolt.Tests.Helpers;

public static class TestEmail
{
    public static string Create(string domain = "example.com") =>
        $"test-{Guid.NewGuid():N}@{domain}";
}
