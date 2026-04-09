namespace Fasolt.Server.Application.Auth;

public enum VerifyResult { Ok, Incorrect, Expired, LockedOut, NotFound }
public enum ResendResult { Ok, TooSoon, TooManyAttempts }

public interface IEmailVerificationCodeService
{
    Task<string> GenerateAndStoreAsync(string userId, CancellationToken cancellationToken);
    Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken cancellationToken);
    Task<ResendResult> CanResendAsync(string userId, CancellationToken cancellationToken);
}
