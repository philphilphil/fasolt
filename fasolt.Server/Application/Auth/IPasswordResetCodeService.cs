namespace Fasolt.Server.Application.Auth;

public interface IPasswordResetCodeService
{
    Task<string> GenerateAndStoreAsync(string userId, CancellationToken cancellationToken);
    Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken cancellationToken);
    Task<ResendResult> CanResendAsync(string userId, CancellationToken cancellationToken);
}
