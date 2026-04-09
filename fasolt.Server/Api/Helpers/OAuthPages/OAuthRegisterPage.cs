using System.Web;

namespace Fasolt.Server.Api.Helpers.OAuthPages;

internal static class OAuthRegisterPage
{
    private const string ExtraStyles = """
                    .rules { margin-top: 6px; font-size: 0.75rem; color: #71717a; list-style: none; padding-left: 0; }
                    .rules li { padding: 2px 0; }
                    .rules li.ok { color: #059669; }
                    .rules li.ok::before { content: "✓ "; }
                    .rules li.pending::before { content: "○ "; }
                    .mismatch { color: #b91c1c; font-size: 0.75rem; margin-top: 4px; }
                    .tos { display: flex; align-items: flex-start; gap: 8px; margin: 12px 0; font-size: 0.8125rem; color: #374151; }
                    .tos input { margin-top: 2px; width: auto; }
                    .tos a { color: #18181b; font-weight: 500; }
                    @media (prefers-color-scheme: dark) {
                        .tos { color: #d4d4d8; }
                        .tos a { color: #fafafa; }
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

    // NOTE: Keep the rule list below in sync with the password policy
    // configured in Program.cs (IdentityOptions.Password). Drift here
    // means the client-side checklist lies to the user. Tracked separately
    // from #107.
    public static string Render(string csrfToken, string returnUrl, string? error)
    {
        var csrf = HttpUtility.HtmlAttributeEncode(csrfToken);
        var returnUrlAttr = HttpUtility.HtmlAttributeEncode(returnUrl);

        var body = $$"""
            <main class="card">
                <div class="header">
                    <h1>Create your Fasolt account</h1>
                </div>
                {{OAuthPageLayout.ErrorBlock(error)}}
                <form method="post" action="/oauth/register" id="registerForm">
                    <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                    <input type="hidden" name="returnUrl" value="{{returnUrlAttr}}" />
                    <div class="field">
                        <label for="email">Email</label>
                        <input type="email" id="email" name="email" placeholder="you@example.com" autocomplete="email" required autofocus />
                    </div>
                    <div class="field">
                        <label for="password">Password</label>
                        <input type="password" id="password" name="password" autocomplete="new-password" required />
                        <ul class="rules" id="rules">
                            <li class="pending" data-rule="length">At least 8 characters</li>
                            <li class="pending" data-rule="upper">Uppercase letter</li>
                            <li class="pending" data-rule="lower">Lowercase letter</li>
                            <li class="pending" data-rule="digit">Number</li>
                        </ul>
                    </div>
                    <div class="field">
                        <label for="confirmPassword">Confirm password</label>
                        <input type="password" id="confirmPassword" name="confirmPassword" autocomplete="new-password" required />
                        <div class="mismatch" id="mismatch" style="display:none">Passwords don't match</div>
                    </div>
                    <label class="tos">
                        <input type="checkbox" name="tosAccepted" value="true" id="tos" required />
                        <span>I agree to the <a href="/terms" target="_blank">Terms of Service</a></span>
                    </label>
                    <button type="submit" id="submit">Create account</button>
                </form>
                <p class="footer">Already have an account? <a href="/oauth/login?returnUrl={{returnUrlAttr}}">Sign in</a></p>
            </main>
            """;

        return OAuthPageLayout.Render("Create account", body, ExtraStyles, EvalScript);
    }
}
