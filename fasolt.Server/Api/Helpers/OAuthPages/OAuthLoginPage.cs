using System.Web;

namespace Fasolt.Server.Api.Helpers.OAuthPages;

internal static class OAuthLoginPage
{
    // Inline SVG logo so this page works regardless of static-file routing.
    // Mirrors fasolt.client/public/favicon.svg.
    private const string LogoSvg = """
        <svg viewBox="0 0 80 80" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
          <g transform="rotate(-14 40 44)"><rect x="12" y="22" width="32" height="24" rx="5" fill="#e8f1fc" stroke="#93c5fd" stroke-width="1.5"/></g>
          <g transform="rotate(14 40 44)"><rect x="36" y="22" width="32" height="24" rx="5" fill="#e8f1fc" stroke="#93c5fd" stroke-width="1.5"/></g>
          <rect x="23" y="26" width="34" height="24" rx="5" fill="#dbeafe" stroke="#0969da" stroke-width="1.5"/>
          <line x1="30" y1="34" x2="50" y2="34" stroke="#0969da" stroke-opacity="0.45" stroke-width="1.5" stroke-linecap="round"/>
          <line x1="30" y1="39" x2="44" y2="39" stroke="#0969da" stroke-opacity="0.45" stroke-width="1.5" stroke-linecap="round"/>
          <line x1="34" y1="66" x2="50" y2="60" stroke="#0969da" stroke-width="1.5" stroke-linecap="round" opacity="0.28"/>
          <line x1="50" y1="60" x2="64" y2="52" stroke="#0969da" stroke-width="1.5" stroke-linecap="round" opacity="0.28"/>
          <circle cx="34" cy="66" r="2.5" fill="#0969da" opacity="0.4"/>
          <circle cx="50" cy="60" r="3" fill="#0969da" opacity="0.65"/>
          <circle cx="64" cy="52" r="3.5" fill="#3b82f6" opacity="0.92"/>
        </svg>
        """;

    private const string ExtraStyles = """
                    .or-divider {
                        display: flex;
                        align-items: center;
                        gap: 12px;
                        margin: 12px 0;
                        color: #a1a1aa;
                        font-size: 0.75rem;
                    }
                    .or-divider::before, .or-divider::after { content: ''; flex: 1; height: 1px; background: #e5e7eb; }
                    .btn-github {
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        gap: 8px;
                        width: 100%;
                        padding: 11px;
                        background: #24292f;
                        color: white;
                        border: none;
                        border-radius: 8px;
                        cursor: pointer;
                        font-size: 0.9375rem;
                        font-weight: 500;
                        text-decoration: none;
                        transition: background 0.15s;
                    }
                    .btn-github:hover { background: #32383f; }
                    .btn-github:active { background: #1b1f23; }
                    @media (prefers-color-scheme: dark) {
                        .or-divider { color: #71717a; }
                        .or-divider::before, .or-divider::after { background: #27272a; }
                        .btn-github { background: #fafafa; color: #18181b; }
                        .btn-github:hover { background: #e4e4e7; }
                        .btn-github:active { background: #d4d4d8; }
                    }
        """;

    public static string Render(string csrfToken, string returnUrl, string? error, bool gitHubEnabled)
    {
        var csrf = HttpUtility.HtmlAttributeEncode(csrfToken);
        var returnUrlAttr = HttpUtility.HtmlAttributeEncode(returnUrl);
        var returnUrlParam = Uri.EscapeDataString(returnUrl);

        var gitHubHtml = gitHubEnabled ? $$"""
            <a href="/api/account/github-login?returnUrl={{returnUrlParam}}" class="btn-github">
                <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
                Continue with GitHub
            </a>
            <div class="or-divider"><span>or</span></div>
        """ : "";

        var body = $$"""
            <main class="card">
                <div class="header">
                    {{LogoSvg}}
                    <h1>Sign in to fasolt</h1>
                </div>
                {{OAuthPageLayout.ErrorBlock(error)}}
                {{gitHubHtml}}
                <form method="post" action="/oauth/login">
                    <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                    <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                    <div class="field">
                        <label for="email">Email</label>
                        <input type="email" id="email" name="email" placeholder="you@example.com" autocomplete="email" required autofocus />
                    </div>
                    <div class="field">
                        <label for="password">Password</label>
                        <input type="password" id="password" name="password" autocomplete="current-password" required />
                    </div>
                    <button type="submit">Sign in</button>
                </form>
                <p class="footer">New to Fasolt? <a href="/oauth/register?returnUrl={{returnUrlAttr}}">Create an account</a></p>
            </main>
            """;

        return OAuthPageLayout.Render("Sign in", body, ExtraStyles);
    }
}
