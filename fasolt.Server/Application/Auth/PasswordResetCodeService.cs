using System.Text;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Auth;

public class PasswordResetCodeService : IPasswordResetCodeService
{
    // 15 minutes: password reset is higher-stakes than email verification,
    // so keep the window as short as the spec allows without making the
    // inbox-to-browser round trip user-hostile.
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    private readonly OtpCodeStore<PasswordResetCode> _store;

    public PasswordResetCodeService(AppDbContext db, string pepper, TimeProvider time)
    {
        _store = new OtpCodeStore<PasswordResetCode>(
            db,
            Encoding.UTF8.GetBytes(pepper),
            time,
            ctx => ctx.PasswordResetCodes,
            CodeLifetime);
    }

    public Task<string> GenerateAndStoreAsync(string userId, CancellationToken cancellationToken)
        => _store.GenerateAndStoreAsync(userId, cancellationToken);

    public Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken cancellationToken)
        => _store.VerifyAsync(userId, code, cancellationToken);

    public Task<ResendResult> CanResendAsync(string userId, CancellationToken cancellationToken)
        => _store.CanResendAsync(userId, cancellationToken);
}
