namespace Fasolt.Server.Domain.Entities;

public class PasswordResetCode : IOtpCodeEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;

    /// <summary>
    /// HMAC-SHA256 of the 6-digit code using the server pepper.
    /// Hex-encoded for convenient equality comparison.
    /// </summary>
    public string CodeHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Failed verify attempts against the current code. Resets on resend.</summary>
    public int Attempts { get; set; }

    /// <summary>Total sends in this reset session. Never resets until row is deleted.</summary>
    public int SentCount { get; set; }

    public DateTimeOffset LastSentAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
