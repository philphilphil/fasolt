namespace Fasolt.Server.Api.Middleware;

/// <summary>
/// Sets a strict Content-Security-Policy header on responses that flow
/// through this middleware. Registered via UseWhen scoped to /oauth/* so
/// only the server-rendered auth pages carry the header — the SPA bundle
/// and API endpoints are unaffected.
///
/// Policy highlights:
/// - default-src 'self' — only same-origin resources by default
/// - style-src 'self'  — no inline styles (auth.css is external)
/// - script-src 'self' — no inline scripts (password-rules.js is external)
/// - form-action 'self' https://github.com — allow GitHub OAuth redirects
/// - frame-ancestors 'none' — clickjacking defense for consent page
/// - img-src 'self' data: — inline SVG in data: URIs
/// - base-uri 'self' — lock down &lt;base&gt; tag injection
/// </summary>
public class ContentSecurityPolicyMiddleware
{
    private readonly RequestDelegate _next;

    private const string PolicyHeaderValue =
        "default-src 'self'; " +
        "style-src 'self'; " +
        "script-src 'self'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "form-action 'self' https://github.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'";

    public ContentSecurityPolicyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
            {
                context.Response.Headers["Content-Security-Policy"] = PolicyHeaderValue;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
