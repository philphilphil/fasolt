using System.Web;

namespace Fasolt.Server.Api.Helpers;

// Shared chrome for the server-rendered OAuth pages (/oauth/login,
// /oauth/register, /oauth/verify-email, /oauth/consent). These pages must
// render without any client-side bundle — ASWebAuthenticationSession hits
// them cold — so we stay on raw HTML strings rather than moving to Razor
// or Blazor (per #107 non-goals).
//
// Pages call Render() with their title, body markup, and optional
// page-specific CSS / script. BaseStyles is the single source of truth for
// the palette, form controls, error block, and dark-mode rules.
internal static class OAuthPageLayout
{
    private const string BaseStyles = """
                    * { box-sizing: border-box; margin: 0; padding: 0; }
                    html, body { height: 100%; }
                    body {
                        font-family: -apple-system, system-ui, sans-serif;
                        background: #fafafa;
                        color: #18181b;
                        display: flex;
                        align-items: flex-start;
                        justify-content: center;
                        padding: max(env(safe-area-inset-top), 16px) 16px max(env(safe-area-inset-bottom), 16px);
                        -webkit-font-smoothing: antialiased;
                    }
                    @media (min-height: 640px) { body { align-items: center; } }
                    .card {
                        width: 100%;
                        max-width: 380px;
                        background: white;
                        border: 1px solid #e5e7eb;
                        border-radius: 14px;
                        padding: 24px 22px;
                        box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
                    }
                    .header { display: flex; flex-direction: column; align-items: center; gap: 8px; margin-bottom: 18px; }
                    .header svg { width: 48px; height: 48px; }
                    .header h1 { font-size: 1.125rem; font-weight: 600; letter-spacing: -0.01em; color: #18181b; }
                    .header p { font-size: 0.8125rem; color: #71717a; margin-top: -2px; }
                    label { display: block; font-size: 0.75rem; font-weight: 500; color: #374151; margin-bottom: 4px; }
                    input[type=email], input[type=password], input[type=text] {
                        width: 100%;
                        padding: 10px 12px;
                        border: 1px solid #d1d5db;
                        border-radius: 8px;
                        font-size: 0.9375rem;
                        outline: none;
                        background: white;
                        transition: border-color 0.15s, box-shadow 0.15s;
                        -webkit-appearance: none;
                    }
                    input:focus { border-color: #18181b; box-shadow: 0 0 0 3px rgba(24, 24, 27, 0.08); }
                    .field { margin-bottom: 10px; }
                    button {
                        width: 100%;
                        padding: 11px;
                        margin-top: 4px;
                        background: #18181b;
                        color: white;
                        border: none;
                        border-radius: 8px;
                        cursor: pointer;
                        font-size: 0.9375rem;
                        font-weight: 500;
                        transition: background 0.15s;
                    }
                    button:hover { background: #27272a; }
                    button:active { background: #09090b; }
                    button:disabled { background: #a1a1aa; cursor: not-allowed; }
                    .error {
                        color: #b91c1c;
                        font-size: 0.8125rem;
                        margin-bottom: 10px;
                        padding: 8px 12px;
                        background: #fef2f2;
                        border: 1px solid #fecaca;
                        border-radius: 8px;
                    }
                    .footer { text-align: center; margin-top: 14px; font-size: 0.75rem; color: #a1a1aa; }
                    .footer a { color: inherit; font-weight: 500; text-decoration: none; }
                    @media (prefers-color-scheme: dark) {
                        body { background: #0a0a0a; color: #fafafa; }
                        .card { background: #18181b; border-color: #27272a; }
                        .header h1 { color: #fafafa; }
                        .header p { color: #a1a1aa; }
                        label { color: #d4d4d8; }
                        input[type=email], input[type=password], input[type=text] { background: #0a0a0a; border-color: #3f3f46; color: #fafafa; }
                        input:focus { border-color: #fafafa; box-shadow: 0 0 0 3px rgba(250, 250, 250, 0.08); }
                        button { background: #fafafa; color: #18181b; }
                        button:hover { background: #e4e4e7; }
                        button:active { background: #d4d4d8; }
                        .error { background: #450a0a; border-color: #7f1d1d; color: #fecaca; }
                        .footer { color: #71717a; }
                    }
        """;

    public static string Render(string title, string bodyContent, string extraStyles = "", string bodyScript = "")
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
                <title>{{title}} — fasolt</title>
                <style>
            {{BaseStyles}}
            {{extraStyles}}
                </style>
            </head>
            <body>
            {{bodyContent}}
            {{bodyScript}}
            </body>
            </html>
            """;
    }

    public static string ErrorBlock(string? error)
        => string.IsNullOrEmpty(error)
            ? ""
            : $"<p class=\"error\">{HttpUtility.HtmlEncode(error)}</p>";
}
