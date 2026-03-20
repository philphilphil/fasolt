using Microsoft.AspNetCore.Identity;
using SpacedMd.Server.Domain.Entities;

namespace SpacedMd.Server.Infrastructure.Services;

public class DevEmailSender : IEmailSender<AppUser>
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        _logger.LogInformation("Confirmation link for {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        _logger.LogInformation("Password reset link for {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
    {
        _logger.LogInformation("Password reset code for {Email}: {Code}", email, resetCode);
        return Task.CompletedTask;
    }
}
