using System.Web;

namespace Fasolt.Server.Api.Helpers.OAuthPages;

internal static class OAuthForgotPasswordPage
{
    private const string ExtraStyles = """
                    .card { padding: 32px 24px; }
                    .card h1 { font-size: 1.25rem; font-weight: 600; margin-bottom: 8px; color: #18181b; text-align: center; }
                    .card > p { color: #71717a; font-size: 0.8125rem; margin-bottom: 20px; text-align: center; }
                    @media (prefers-color-scheme: dark) {
                        .card h1 { color: #fafafa; }
                        .card > p { color: #a1a1aa; }
                    }
        """;

    public static string Render(string csrfToken, string returnUrl, string? error, bool sent, string? email)
    {
        var csrf = HttpUtility.HtmlAttributeEncode(csrfToken);
        var returnUrlAttr = HttpUtility.HtmlAttributeEncode(returnUrl);
        var emailDisplay = HttpUtility.HtmlEncode(email ?? "");

        // After submit we always show a generic "check your inbox" confirmation,
        // regardless of whether the email is a real account — this keeps the
        // endpoint from being an enumeration oracle. The confirmation offers a
        // link straight to the reset-password page so the user can paste the
        // code from their email.
        if (sent)
        {
            var body = $$"""
                <main class="card">
                    <h1>Check your email</h1>
                    <p>If <strong>{{emailDisplay}}</strong> matches an account, we sent a 6-digit reset code.</p>
                    <form method="get" action="/oauth/reset-password">
                        <input type="hidden" name="email" value="{{HttpUtility.HtmlAttributeEncode(email ?? "")}}" />
                        <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                        <button type="submit">Enter reset code</button>
                    </form>
                    <p class="footer"><a href="/oauth/login?returnUrl={{returnUrlAttr}}">Back to sign in</a></p>
                </main>
                """;

            return OAuthPageLayout.Render("Check your email", body, ExtraStyles);
        }

        var formBody = $$"""
            <main class="card">
                <h1>Reset your password</h1>
                <p>Enter your email and we'll send you a 6-digit code.</p>
                {{OAuthPageLayout.ErrorBlock(error)}}
                <form method="post" action="/oauth/forgot-password">
                    <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                    <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                    <div class="field">
                        <label for="email">Email</label>
                        <input type="email" id="email" name="email" placeholder="you@example.com" autocomplete="email" required autofocus />
                    </div>
                    <button type="submit">Send reset code</button>
                </form>
                <p class="footer"><a href="/oauth/login?returnUrl={{returnUrlAttr}}">Back to sign in</a></p>
            </main>
            """;

        return OAuthPageLayout.Render("Reset password", formBody, ExtraStyles);
    }
}
