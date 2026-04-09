using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Services;

public class DevEmailSender : IEmailSender<AppUser>, IOtpEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly TestEmailSink? _sink;

    public DevEmailSender(ILogger<DevEmailSender> logger, TestEmailSink? sink = null)
    {
        _logger = logger;
        _sink = sink;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        _logger.LogWarning("[DEV EMAIL] Confirmation link for {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    // SendPasswordResetLinkAsync is part of IEmailSender<AppUser> (ASP.NET Core
    // Identity). The app uses OTP codes (SendPasswordResetCodeAsync) instead,
    // so this is a no-op kept only to satisfy the interface.
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
        => Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
    {
        _logger.LogWarning("[DEV EMAIL] Password reset code for {Email}: {Code}", email, resetCode);
        _sink?.Capture(email, "Your Fasolt password reset code", resetCode);
        return Task.CompletedTask;
    }

    public Task SendVerificationCodeAsync(AppUser user, string email, string code)
    {
        _logger.LogWarning("[DEV EMAIL] Verification code for {Email}: {Code}", email, code);
        _sink?.Capture(email, "Verify your Fasolt email", code);
        return Task.CompletedTask;
    }
}
