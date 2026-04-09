using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Auth;

public class EmailVerificationCodeService : IEmailVerificationCodeService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 5;
    private const int MaxSendsPerSession = 5;

    private readonly AppDbContext _db;
    private readonly byte[] _pepper;
    private readonly TimeProvider _time;

    public EmailVerificationCodeService(AppDbContext db, string pepper, TimeProvider time)
    {
        _db = db;
        _pepper = Encoding.UTF8.GetBytes(pepper);
        _time = time;
    }

    public async Task<string> GenerateAndStoreAsync(string userId, CancellationToken cancellationToken)
    {
        // Serialize concurrent resends for this user. Without this, two
        // callers can both pass CanResendAsync, both read SentCount=N, and
        // both write N+1 — bypassing the cap and cooldown by one on each
        // simultaneous click. A per-user Postgres advisory lock held for the
        // transaction's lifetime is the cheapest fix: no schema change, no
        // global contention. We also re-check cap + cooldown inside the lock
        // because CanResendAsync (outside the lock) is only advisory.
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({userId})::bigint)", cancellationToken);

        var now = _time.GetUtcNow();
        var code = GenerateCode();
        var hash = Hash(code);

        var existing = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        // Expire a stale row: once the code has expired AND any active lockout
        // has elapsed, the row is garbage from the user's point of view. Treat
        // it as if no row existed so SentCount isn't a permanent dead-end.
        if (existing is not null && IsStale(existing, now))
        {
            _db.EmailVerificationCodes.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
            existing = null;
        }

        if (existing is null)
        {
            _db.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                CodeHash = hash,
                ExpiresAt = now.Add(CodeLifetime),
                Attempts = 0,
                SentCount = 1,
                LastSentAt = now,
                LockedUntil = null,
            });
        }
        else
        {
            if (existing.SentCount >= MaxSendsPerSession)
                throw new InvalidOperationException(
                    "Cannot generate another code: session send cap exceeded. Call CanResendAsync first.");

            if (now - existing.LastSentAt < ResendCooldown)
                throw new InvalidOperationException(
                    "Cannot generate another code: resend cooldown in effect. Call CanResendAsync first.");

            existing.CodeHash = hash;
            existing.ExpiresAt = now.Add(CodeLifetime);
            existing.Attempts = 0;
            existing.SentCount += 1;
            existing.LastSentAt = now;
            existing.LockedUntil = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return code;
    }

    public async Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var row = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (row is null)
            return VerifyResult.NotFound;

        if (row.LockedUntil is { } lockedUntil && lockedUntil > now)
            return VerifyResult.LockedOut;

        // Lockout has expired — give the user a fresh attempt counter
        if (row.LockedUntil is not null)
        {
            row.LockedUntil = null;
            row.Attempts = 0;
            // Note: we don't SaveChanges yet — the hash compare below will
            // SaveChanges either way (on match via Remove, on mismatch via
            // increment). The reset travels with whichever path fires.
        }

        if (row.ExpiresAt <= now)
            return VerifyResult.Expired;

        var submittedHash = Hash(code);
        var stored = Convert.FromHexString(row.CodeHash);
        var submitted = Convert.FromHexString(submittedHash);

        if (CryptographicOperations.FixedTimeEquals(stored, submitted))
        {
            _db.EmailVerificationCodes.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
            return VerifyResult.Ok;
        }

        row.Attempts += 1;
        if (row.Attempts >= MaxAttempts)
        {
            row.LockedUntil = now.Add(LockoutDuration);
            await _db.SaveChangesAsync(cancellationToken);
            return VerifyResult.LockedOut;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return VerifyResult.Incorrect;
    }

    public async Task<ResendResult> CanResendAsync(string userId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var row = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (row is null)
            return ResendResult.Ok;

        // A stale row (expired code + lockout elapsed) is indistinguishable
        // from a fresh session — let the caller resend. GenerateAndStoreAsync
        // will clean up the row when it runs.
        if (IsStale(row, now))
            return ResendResult.Ok;

        // Block resends while the user is in an active lockout window.
        // Otherwise a locked-out user can still burn through SentCount on
        // resend clicks, permanently stranding their session cap.
        if (row.LockedUntil is { } lockedUntil && lockedUntil > now)
            return ResendResult.LockedOut;

        if (row.SentCount >= MaxSendsPerSession)
            return ResendResult.TooManyAttempts;

        if (now - row.LastSentAt < ResendCooldown)
            return ResendResult.TooSoon;

        return ResendResult.Ok;
    }

    // A row is "stale" once the OTP has expired AND any lockout has passed.
    // From the user's perspective there's nothing left to act on, so we can
    // safely drop it and let them start a fresh verification session.
    private static bool IsStale(EmailVerificationCode row, DateTimeOffset now)
    {
        if (row.ExpiresAt > now) return false;
        if (row.LockedUntil is { } lockedUntil && lockedUntil > now) return false;
        return true;
    }

    private static string GenerateCode()
    {
        while (true)
        {
            var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
            if (n == 0) continue; // avoid visually confusing "000000"
            return n.ToString("D6");
        }
    }

    private string Hash(string code)
    {
        using var hmac = new HMACSHA256(_pepper);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
