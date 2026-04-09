using System.Web;

namespace Fasolt.Server.Api.Helpers.OAuthPages;

internal static class OAuthResetPasswordPage
{
    private const string ExtraStyles = """
                    .card { padding: 32px 24px; }
                    .card h1 { font-size: 1.25rem; font-weight: 600; margin-bottom: 8px; color: #18181b; text-align: center; }
                    .card > p.lead { color: #71717a; font-size: 0.8125rem; margin-bottom: 20px; text-align: center; }
                    .card > p.lead strong { color: #18181b; }
                    input[name=code] {
                        padding: 14px;
                        font-size: 1.5rem;
                        text-align: center;
                        letter-spacing: 0.5em;
                        font-family: ui-monospace, "SF Mono", Menlo, monospace;
                        border-radius: 10px;
                    }
                    .rules { margin-top: 6px; font-size: 0.75rem; color: #71717a; list-style: none; padding-left: 0; }
                    .rules li { padding: 2px 0; }
                    .rules li.ok { color: #059669; }
                    .rules li.ok::before { content: "\u2713 "; }
                    .rules li.pending::before { content: "\u25CB "; }
                    .mismatch { color: #b91c1c; font-size: 0.75rem; margin-top: 4px; }
                    .resend { margin-top: 14px; font-size: 0.8125rem; color: #71717a; text-align: center; }
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
                        .card > p.lead { color: #a1a1aa; }
                        .card > p.lead strong { color: #fafafa; }
                        .resend { color: #a1a1aa; }
                        .resend-inline-button { color: #fafafa; }
                    }
        """;

    private const string EvalScript = """
        <script>
            const pwd = document.getElementById('password');
            const confirm = document.getElementById('confirmPassword');
            const rules = document.getElementById('rules');
            const mismatch = document.getElementById('mismatch');
            function evaluate() {
                const v = pwd.value;
                const checks = {
                    length: v.length >= 8,
                    upper: /[A-Z]/.test(v),
                    lower: /[a-z]/.test(v),
                    digit: /[0-9]/.test(v)
                };
                for (const li of rules.children) {
                    const r = li.dataset.rule;
                    li.className = checks[r] ? 'ok' : 'pending';
                }
                mismatch.style.display = (confirm.value && confirm.value !== v) ? 'block' : 'none';
            }
            pwd.addEventListener('input', evaluate);
            confirm.addEventListener('input', evaluate);
        </script>
        """;

    public static string Render(string csrfToken, string email, string returnUrl, string? error, bool success)
    {
        var csrf = HttpUtility.HtmlAttributeEncode(csrfToken);
        var emailAttr = HttpUtility.HtmlAttributeEncode(email);
        var emailDisplay = HttpUtility.HtmlEncode(email);
        var returnUrlAttr = HttpUtility.HtmlAttributeEncode(returnUrl);

        if (success)
        {
            var okBody = $$"""
                <main class="card">
                    <h1>Password updated</h1>
                    <p class="lead">You can now sign in with your new password.</p>
                    <form method="get" action="/oauth/login">
                        <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                        <button type="submit">Go to sign in</button>
                    </form>
                </main>
                """;
            return OAuthPageLayout.Render("Password updated", okBody, ExtraStyles);
        }

        var body = $$"""
            <main class="card">
                <h1>Enter reset code</h1>
                <p class="lead">We sent a 6-digit code to <strong>{{emailDisplay}}</strong></p>
                {{OAuthPageLayout.ErrorBlock(error)}}
                <form method="post" action="/oauth/reset-password" id="resetForm">
                    <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                    <input type="hidden" name="email" value="{{emailAttr}}" />
                    <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                    <div class="field">
                        <input type="text" name="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" maxlength="6" autofocus required />
                    </div>
                    <div class="field">
                        <label for="password">New password</label>
                        <input type="password" id="password" name="password" autocomplete="new-password" required />
                        <ul class="rules" id="rules">
                            <li class="pending" data-rule="length">At least 8 characters</li>
                            <li class="pending" data-rule="upper">Uppercase letter</li>
                            <li class="pending" data-rule="lower">Lowercase letter</li>
                            <li class="pending" data-rule="digit">Number</li>
                        </ul>
                    </div>
                    <div class="field">
                        <label for="confirmPassword">Confirm new password</label>
                        <input type="password" id="confirmPassword" name="confirmPassword" autocomplete="new-password" required />
                        <div class="mismatch" id="mismatch" style="display:none">Passwords don't match</div>
                    </div>
                    <button type="submit">Reset password</button>
                </form>
                <div class="resend">
                    Didn't get it?
                    <form method="post" action="/oauth/reset-password/resend">
                        <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                        <input type="hidden" name="email" value="{{emailAttr}}" />
                        <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                        <button type="submit" class="resend-inline-button">Resend code</button>
                    </form>
                </div>
                <p class="footer"><a href="/oauth/forgot-password?returnUrl={{returnUrlAttr}}">Use a different email</a></p>
            </main>
            """;

        return OAuthPageLayout.Render("Reset password", body, ExtraStyles, EvalScript);
    }
}
