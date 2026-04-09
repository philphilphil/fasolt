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
        var now = _time.GetUtcNow();
        var code = GenerateCode();
        var hash = Hash(code);

        var existing = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

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
            existing.CodeHash = hash;
            existing.ExpiresAt = now.Add(CodeLifetime);
            existing.Attempts = 0;
            existing.SentCount += 1;
            existing.LastSentAt = now;
            existing.LockedUntil = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
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

        if (row.SentCount >= MaxSendsPerSession)
            return ResendResult.TooManyAttempts;

        if (now - row.LastSentAt < ResendCooldown)
            return ResendResult.TooSoon;

        return ResendResult.Ok;
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
