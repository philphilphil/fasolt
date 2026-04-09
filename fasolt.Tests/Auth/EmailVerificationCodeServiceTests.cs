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
    public async Task GenerateAndStoreAsync_RejectsResend_AfterFiveSends()
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

    private (EmailVerificationCodeService service, AppDbContext db) CreateService(TimeProvider? time = null)
    {
        var db = _db.CreateDbContext();
        var service = new EmailVerificationCodeService(db, Pepper, time ?? TimeProvider.System);
        return (service, db);
    }
}
