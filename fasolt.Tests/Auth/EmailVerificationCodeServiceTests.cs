using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

public class EmailVerificationCodeServiceTests : IAsyncLifetime
{
    private const string Pepper = "test-pepper-not-for-production";
    private readonly TestDb _db = new();

    public Task InitializeAsync() => _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GenerateAndStoreAsync_ReturnsSixDigitCode_AndPersistsRow()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        code.Should().MatchRegex(@"^\d{6}$");

        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleAsync(r => r.UserId == _db.UserId);
        row.CodeHash.Should().NotBeEmpty();
        row.CodeHash.Should().NotContain(code, "code must be hashed, not stored plaintext");
        row.SentCount.Should().Be(1);
        row.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsOk_ForCorrectCode_AndDeletesRow()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        var result = await service.VerifyAsync(_db.UserId, code, CancellationToken.None);

        result.Should().Be(VerifyResult.Ok);

        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleOrDefaultAsync(r => r.UserId == _db.UserId);
        row.Should().BeNull("row must be deleted on successful verify");
    }

    [Fact]
    public async Task VerifyAsync_ReturnsIncorrect_ForWrongCode_AndIncrementsAttempts()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        var result = await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);

        result.Should().Be(VerifyResult.Incorrect);

        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleAsync(r => r.UserId == _db.UserId);
        row.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAsync_LocksOut_AfterFiveWrongAttempts()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);

        var result = await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);
        result.Should().Be(VerifyResult.LockedOut);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsExpired_AfterExpiryTime()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(16));

        var result = await service.VerifyAsync(_db.UserId, code, CancellationToken.None);
        result.Should().Be(VerifyResult.Expired);
    }

    [Fact]
    public async Task CanResendAsync_ReturnsTooSoon_Within30Seconds()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        var result = await service.CanResendAsync(_db.UserId, CancellationToken.None);
        result.Should().Be(ResendResult.TooSoon);
    }

    [Fact]
    public async Task CanResendAsync_ReturnsOk_After30Seconds()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(31));

        var result = await service.CanResendAsync(_db.UserId, CancellationToken.None);
        result.Should().Be(ResendResult.Ok);
    }

    [Fact]
    public async Task CanResendAsync_ReturnsTooManyAttempts_AfterFiveSends()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        for (var i = 0; i < 5; i++)
        {
            await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(31));
        }

        var result = await service.CanResendAsync(_db.UserId, CancellationToken.None);
        result.Should().Be(ResendResult.TooManyAttempts);
    }

    [Fact]
    public async Task GenerateAndStoreAsync_Throws_WhenSessionCapExceeded()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        // Fill the session cap
        for (var i = 0; i < 5; i++)
        {
            await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(31));
        }

        // The 6th call should throw
        var act = async () => await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*session send cap*");
    }

    [Fact]
    public async Task VerifyAsync_ResetsAttempts_AfterLockoutExpires()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        // Lock the user out with 5 wrong attempts
        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);

        // Still within the lockout window
        time.Advance(TimeSpan.FromMinutes(5));
        (await service.VerifyAsync(_db.UserId, code, CancellationToken.None))
            .Should().Be(VerifyResult.LockedOut);

        // Past the lockout window — the correct code should now work
        time.Advance(TimeSpan.FromMinutes(6));
        (await service.VerifyAsync(_db.UserId, code, CancellationToken.None))
            .Should().Be(VerifyResult.Ok);
    }

    [Fact]
    public async Task VerifyAsync_GivesFreshAttemptsAfterLockoutExpires_NotSingleShot()
    {
        // This is the real regression test for the re-lock-on-first-wrong-attempt
        // bug: pre-fix, once Attempts hit 5 and LockedUntil passed, the very next
        // wrong attempt would return LockedOut again (because Attempts was still 5
        // and incremented to 6, immediately re-locking). The fix resets Attempts
        // to 0 on lockout expiry so the user gets a genuine fresh 5-attempt window.
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        // Lock the user out
        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);

        // Advance past the lockout window
        time.Advance(TimeSpan.FromMinutes(11));

        // A wrong code should return Incorrect (not LockedOut) — i.e. the user
        // has fresh attempts. Pre-fix this would return LockedOut.
        (await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None))
            .Should().Be(VerifyResult.Incorrect);

        // And the row's Attempts should be 1 (fresh count), not 6
        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleAsync(r => r.UserId == _db.UserId);
        row.Attempts.Should().Be(1);
        row.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotFound_WhenNoRowExists()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var result = await service.VerifyAsync(_db.UserId, "123456", CancellationToken.None);
        result.Should().Be(VerifyResult.NotFound);
    }

    private (EmailVerificationCodeService service, AppDbContext db) CreateService(TimeProvider? time = null)
    {
        var db = _db.CreateDbContext();
        var service = new EmailVerificationCodeService(db, Pepper, time ?? TimeProvider.System);
        return (service, db);
    }
}
