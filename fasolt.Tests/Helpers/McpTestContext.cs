using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Fasolt.Tests.Helpers;

/// <summary>
/// The ambient HTTP context an MCP tool runs in. Tools resolve their caller from
/// it via <c>McpUserResolver</c> and build deep links from the request host, so a
/// tool-level test has to supply both.
/// </summary>
internal static class McpTestContext
{
    public static IHttpContextAccessor For(string userId, string host = "fasolt.app", string scheme = "https")
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test")),
        };
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);

        return new HttpContextAccessor { HttpContext = context };
    }
}
