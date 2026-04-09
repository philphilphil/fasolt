using System.Web;

namespace Fasolt.Server.Api.Helpers.OAuthPages;

internal static class OAuthVerifyEmailPage
{
    private const string ExtraStyles = """
                    .card { padding: 32px 24px; text-align: center; }
                    .card h1 { font-size: 1.25rem; font-weight: 600; margin-bottom: 8px; color: #18181b; }
                    .card p { color: #71717a; font-size: 0.875rem; margin-bottom: 20px; }
                    .card p strong { color: #18181b; }
                    input[name=code] {
                        padding: 14px;
                        font-size: 1.5rem;
                        text-align: center;
                        letter-spacing: 0.5em;
                        font-family: ui-monospace, "SF Mono", Menlo, monospace;
                        border-radius: 10px;
                    }
                    button { margin-top: 16px; }
                    .resend { margin-top: 16px; font-size: 0.8125rem; color: #71717a; }
                    .resend a { color: #18181b; font-weight: 500; text-decoration: none; }
                    .resend form { display: inline; }
                    .resend-inline-button {
                        display: inline;
                        width: auto;
                        padding: 0;
                        margin: 0;
                        background: transparent;
                        color: #18181b;
                        font-weight: 500;
                        text-decoration: underline;
                        border: none;
                        cursor: pointer;
                        font-size: inherit;
                    }
                    @media (prefers-color-scheme: dark) {
                        .card h1 { color: #fafafa; }
                        .card p { color: #a1a1aa; }
                        .card p strong { color: #fafafa; }
                        .resend { color: #a1a1aa; }
                        .resend a, .resend-inline-button { color: #fafafa; }
                    }
        """;

    public static string Render(string csrfToken, string email, string returnUrl, string? error)
    {
        var csrf = HttpUtility.HtmlAttributeEncode(csrfToken);
        var emailAttr = HttpUtility.HtmlAttributeEncode(email);
        var emailDisplay = HttpUtility.HtmlEncode(email);
        var returnUrlAttr = HttpUtility.HtmlAttributeEncode(returnUrl);

        var body = $$"""
            <main class="card">
                <h1>Check your email</h1>
                <p>We sent a 6-digit code to <strong>{{emailDisplay}}</strong></p>
                {{OAuthPageLayout.ErrorBlock(error)}}
                <form method="post" action="/oauth/verify-email">
                    <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                    <input type="hidden" name="email" value="{{emailAttr}}" />
                    <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                    <input type="text" name="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" maxlength="6" autofocus required />
                    <button type="submit">Verify</button>
                </form>
                <div class="resend">
                    Didn't get it?
                    <form method="post" action="/oauth/verify-email/resend">
                        <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                        <input type="hidden" name="email" value="{{emailAttr}}" />
                        <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                        <button type="submit" class="resend-inline-button">Resend code</button>
                    </form>
                </div>
                <p class="resend"><a href="/oauth/register?returnUrl={{returnUrlAttr}}">Use a different email</a></p>
            </main>
            """;

        return OAuthPageLayout.Render("Verify email", body, ExtraStyles);
    }
}
