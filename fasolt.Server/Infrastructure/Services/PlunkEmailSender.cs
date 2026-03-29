using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Services;

public class PlunkEmailSender : IEmailSender<AppUser>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlunkEmailSender> _logger;
    private readonly string _fromEmail;
    private readonly string _baseUrl;

    public PlunkEmailSender(HttpClient httpClient, ILogger<PlunkEmailSender> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fromEmail = configuration["Plunk:FromEmail"] ?? "noreply@fasolt.app";
        _baseUrl = configuration["App:BaseUrl"]!;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        confirmationLink = RewriteConfirmationLink(confirmationLink);
        var body = $"""
            <p>Welcome to Fasolt!</p>
            <p>Please confirm your email address by clicking the link below:</p>
            <p><a href="{confirmationLink}">Confirm email</a></p>
            <p>If you didn't create an account, you can safely ignore this email.</p>
            """;

        return SendAsync(email, "Confirm your Fasolt account", body);
    }

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        var body = $"""
            <p>You requested a password reset for your Fasolt account.</p>
            <p><a href="{resetLink}">Reset your password</a></p>
            <p>This link expires in 24 hours. If you didn't request this, you can safely ignore this email.</p>
            """;

        return SendAsync(email, "Reset your Fasolt password", body);
    }

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
    {
        var body = $"""
            <p>You requested a password reset for your Fasolt account.</p>
            <p>Your reset code is: <strong>{resetCode}</strong></p>
            <p>This code expires in 24 hours. If you didn't request this, you can safely ignore this email.</p>
            """;

        return SendAsync(email, "Your Fasolt password reset code", body);
    }

    private async Task SendAsync(string to, string subject, string body)
    {
        var payload = new
        {
            to,
            subject,
            body,
            from = _fromEmail
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://next-api.useplunk.com/v1/send", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Plunk email failed ({StatusCode}): {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"Failed to send email via Plunk: {response.StatusCode}");
        }

        _logger.LogInformation("Email sent to {Email}: {Subject}", to, subject);
    }

    private string RewriteConfirmationLink(string confirmationLink)
    {
        if (!confirmationLink.Contains("/api/identity/confirmEmail", StringComparison.OrdinalIgnoreCase))
            return confirmationLink;

        // Identity HTML-encodes the URL (& → &amp;) before passing it here, so decode first
        var decoded = System.Net.WebUtility.HtmlDecode(confirmationLink);

        var queryStart = decoded.IndexOf('?');
        if (queryStart < 0) return confirmationLink;

        var query = QueryHelpers.ParseQuery(decoded[(queryStart + 1)..]);
        if (query.TryGetValue("userId", out var userId) && query.TryGetValue("code", out var code))
            return $"{_baseUrl}/confirm-email?userId={Uri.EscapeDataString(userId!)}&token={Uri.EscapeDataString(code!)}";

        return confirmationLink;
    }
}
