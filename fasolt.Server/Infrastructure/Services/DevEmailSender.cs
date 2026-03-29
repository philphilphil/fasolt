using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Services;

public class DevEmailSender : IEmailSender<AppUser>
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly string _baseUrl;

    public DevEmailSender(ILogger<DevEmailSender> logger, IConfiguration configuration)
    {
        _logger = logger;
        _baseUrl = configuration["App:BaseUrl"]!;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        confirmationLink = RewriteConfirmationLink(confirmationLink);
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

    private string RewriteConfirmationLink(string confirmationLink)
    {
        if (!confirmationLink.Contains("/api/identity/confirmEmail", StringComparison.OrdinalIgnoreCase))
            return confirmationLink;

        var queryStart = confirmationLink.IndexOf('?');
        if (queryStart < 0) return confirmationLink;

        var query = QueryHelpers.ParseQuery(confirmationLink[(queryStart + 1)..]);
        if (query.TryGetValue("userId", out var userId) && query.TryGetValue("code", out var code))
            return $"{_baseUrl}/confirm-email?userId={Uri.EscapeDataString(userId!)}&token={Uri.EscapeDataString(code!)}";

        return confirmationLink;
    }
}
