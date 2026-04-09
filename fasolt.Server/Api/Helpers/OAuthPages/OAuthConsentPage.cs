using System.Web;

namespace Fasolt.Server.Api.Helpers.OAuthPages;

internal static class OAuthConsentPage
{
    private const string ExtraStyles = """
                    .card { padding: 32px; border-radius: 12px; }
                    .logo { font-size: 1.5rem; font-weight: 700; letter-spacing: -0.02em; color: #18181b; }
                    .subtitle { color: #71717a; font-size: 0.875rem; margin-top: 4px; }
                    .divider { height: 1px; background: #e5e7eb; margin: 20px 0; }
                    .app-name { font-weight: 600; color: #18181b; }
                    .prompt { font-size: 0.875rem; color: #374151; text-align: center; margin-bottom: 16px; }
                    .permissions { background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; }
                    .permissions-title { font-size: 0.75rem; font-weight: 500; color: #6b7280; margin-bottom: 8px; }
                    .permissions ul { list-style: none; }
                    .permissions li { font-size: 0.8125rem; color: #374151; padding: 3px 0; }
                    .permissions li::before { content: "\2022"; color: #9ca3af; margin-right: 8px; }
                    button[value=approve] { margin-top: 0; margin-bottom: 8px; }
                    .btn-deny {
                        width: 100%;
                        padding: 11px;
                        background: white;
                        color: #374151;
                        border: 1px solid #d1d5db;
                        border-radius: 8px;
                        cursor: pointer;
                        font-size: 0.9375rem;
                        font-weight: 500;
                        transition: background 0.15s;
                    }
                    .btn-deny:hover { background: #f9fafb; }
                    .btn-deny:active { background: #f3f4f6; }
                    .footer { margin-top: 16px; }
                    @media (prefers-color-scheme: dark) {
                        .logo { color: #fafafa; }
                        .subtitle { color: #a1a1aa; }
                        .divider { background: #27272a; }
                        .app-name { color: #fafafa; }
                        .prompt { color: #d4d4d8; }
                        .permissions { background: #0a0a0a; border-color: #27272a; }
                        .permissions-title { color: #a1a1aa; }
                        .permissions li { color: #d4d4d8; }
                        .permissions li::before { color: #52525b; }
                        .btn-deny { background: #18181b; color: #d4d4d8; border-color: #3f3f46; }
                        .btn-deny:hover { background: #27272a; }
                        .btn-deny:active { background: #0a0a0a; }
                    }
        """;

    public static string Render(string csrfToken, string clientId, string clientName)
    {
        var csrf = HttpUtility.HtmlAttributeEncode(csrfToken);
        var clientIdAttr = HttpUtility.HtmlAttributeEncode(clientId);
        var clientNameDisplay = HttpUtility.HtmlEncode(clientName);

        var body = $$"""
            <main class="card">
                <div class="logo">fasolt</div>
                <p class="subtitle">Authorize application</p>
                <div class="divider"></div>
                <p class="prompt"><span class="app-name">{{clientNameDisplay}}</span> wants to access your account.</p>
                <div class="permissions">
                    <div class="permissions-title">This will allow the application to:</div>
                    <ul>
                        <li>Read and create flashcards and decks</li>
                        <li>View and manage sources</li>
                        <li>Review cards and track study progress</li>
                        <li>Stay signed in and refresh access</li>
                    </ul>
                </div>
                <form method="post" action="/oauth/consent">
                    <input type="hidden" name="__RequestVerificationToken" value="{{csrf}}" />
                    <input type="hidden" name="client_id" value="{{clientIdAttr}}" />
                    <button type="submit" name="decision" value="approve">Authorize</button>
                    <button type="submit" name="decision" value="deny" class="btn-deny">Deny</button>
                </form>
                <p class="footer">You'll be redirected back to your application.</p>
            </main>
            """;

        return OAuthPageLayout.Render("Authorize", body, ExtraStyles);
    }
}
