using System.Collections.Concurrent;

namespace Fasolt.Server.Infrastructure.Services;

/// <summary>
/// Development-only in-memory capture of the last OTP sent to each email.
/// Used by Playwright E2E tests to read back the password-reset /
/// verification codes that would otherwise only land in a real inbox.
///
/// Gated to Development environment in Program.cs — production builds
/// never register this service, and the /api/test/last-email endpoint
/// that reads it is only mapped when IsDevelopment().
/// </summary>
public class TestEmailSink
{
    private readonly ConcurrentDictionary<string, CapturedEmail> _lastByEmail = new();

    public record CapturedEmail(string Email, string Subject, string Code, DateTimeOffset CapturedAt);

    public void Capture(string email, string subject, string code)
    {
        _lastByEmail[email.ToLowerInvariant()] = new CapturedEmail(email, subject, code, DateTimeOffset.UtcNow);
    }

    public CapturedEmail? GetLast(string email)
    {
        _lastByEmail.TryGetValue(email.ToLowerInvariant(), out var captured);
        return captured;
    }
}
