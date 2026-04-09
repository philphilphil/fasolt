namespace Fasolt.Server.Domain.Entities;

/// <summary>
/// Shared shape for single-use OTP rows (email verification, password reset).
/// Lets the OTP store helper in Application.Auth work against any such entity
/// without duplicating throttling/lockout/hashing logic per flow.
/// </summary>
public interface IOtpCodeEntity
{
    int Id { get; set; }
    string UserId { get; set; }

    /// <summary>
    /// HMAC-SHA256 of the 6-digit code using the server pepper.
    /// Hex-encoded for convenient equality comparison.
    /// </summary>
    string CodeHash { get; set; }

    DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Failed verify attempts against the current code. Resets on resend.</summary>
    int Attempts { get; set; }

    /// <summary>Total sends in this session. Never resets until the row is deleted.</summary>
    int SentCount { get; set; }

    DateTimeOffset LastSentAt { get; set; }
    DateTimeOffset? LockedUntil { get; set; }
}
