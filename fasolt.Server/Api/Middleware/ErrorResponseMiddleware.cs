using System.Text.Json;

namespace Fasolt.Server.Api.Middleware;

public class ErrorResponseMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // Only intercept if no body has been written and status indicates an error
        if (context.Response.HasStarted || context.Response.StatusCode < 400)
            return;

        // Don't overwrite responses that already have a body
        if (context.Response.ContentLength.HasValue && context.Response.ContentLength > 0)
            return;

        var (error, message) = context.Response.StatusCode switch
        {
            401 => ("unauthorized", "Authentication required"),
            403 => ("forbidden", "Token expired or insufficient permissions"),
            404 => ("not_found", "Resource not found"),
            _ => ((string?)null, (string?)null)
        };

        if (error is null) return;

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error, message }));
    }
}
