using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Application.Auth;

public interface IOtpEmailSender
{
    Task SendVerificationCodeAsync(AppUser user, string email, string code);
}
