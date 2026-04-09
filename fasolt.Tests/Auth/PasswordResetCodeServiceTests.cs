using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

// Mirrors EmailVerificationCodeServiceTests. Both services share
// OtpCodeStore<T>, but we keep a smaller regression suite here to catch
// wire-up bugs (DbSet accessor, code lifetime) that would only be exercised
// through the PasswordResetCode table.
public class PasswordResetCodeServiceTests : IAsyncLifetime
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
        var row = await read.PasswordResetCodes.SingleAsync(r => r.UserId == _db.UserId);
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
        var row = await read.PasswordResetCodes.SingleOrDefaultAsync(r => r.UserId == _db.UserId);
        row.Should().BeNull("row must be deleted on successful verify so the code is single-use");
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
        var row = await read.PasswordResetCodes.SingleAsync(r => r.UserId == _db.UserId);
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
    public async Task VerifyAsync_ReturnsExpired_After15Minutes()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        // 15-minute code lifetime is the issue-#103 requirement. One second
        // over should flip to Expired.
        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

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
    public async Task EmailVerificationAndPasswordReset_UseIndependentTables()
    {
        // Regression guard: both services share OtpCodeStore<T>, but they
        // must operate on different DbSets. A copy-paste bug where password
        // reset accidentally wrote to EmailVerificationCodes would let a
        // leaked verification code reset the password (and vice versa).
        var (resetService, resetCtx) = CreateService();
        await using var _ = resetCtx;

        var verifyService = new EmailVerificationCodeService(
            _db.CreateDbContext(), Pepper, TimeProvider.System);

        var resetCode = await resetService.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        // The email verification table should still be empty.
        await using var read = _db.CreateDbContext();
        var verifyRow = await read.EmailVerificationCodes.SingleOrDefaultAsync(r => r.UserId == _db.UserId);
        verifyRow.Should().BeNull("password reset must not touch the email verification table");

        var resetRow = await read.PasswordResetCodes.SingleOrDefaultAsync(r => r.UserId == _db.UserId);
        resetRow.Should().NotBeNull("password reset must persist into PasswordResetCodes");

        // The verification service should not recognize a password reset code.
        var crossVerify = await verifyService.VerifyAsync(_db.UserId, resetCode, CancellationToken.None);
        crossVerify.Should().Be(VerifyResult.NotFound,
            "a password-reset code must not be accepted by the email-verification service");
    }

    private (PasswordResetCodeService service, AppDbContext db) CreateService(TimeProvider? time = null)
    {
        var db = _db.CreateDbContext();
        var service = new PasswordResetCodeService(db, Pepper, time ?? TimeProvider.System);
        return (service, db);
    }
}
