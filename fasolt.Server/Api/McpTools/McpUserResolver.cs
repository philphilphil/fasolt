namespace Fasolt.Server.Api.McpTools;

public static class McpUserResolver
{
    public static string GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }
}
