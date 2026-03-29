using FluentAssertions;
using Fasolt.Server.Api.Helpers;

namespace Fasolt.Tests;

public class UrlHelpersTests
{
    [Theory]
    [InlineData("/dashboard", true)]
    [InlineData("/settings?tab=account", true)]
    [InlineData("/cards/123", true)]
    [InlineData("", false)]
    [InlineData("//evil.com", false)]
    [InlineData("/\\evil.com", false)]
    [InlineData("https://evil.com", false)]
    [InlineData("http://evil.com", false)]
    [InlineData("javascript:alert(1)", false)]
    public void IsLocalUrl_ValidatesCorrectly(string url, bool expected)
    {
        UrlHelpers.IsLocalUrl(url).Should().Be(expected);
    }
}
