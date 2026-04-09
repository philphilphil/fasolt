using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Services;

public class PlunkEmailSender : IEmailSender<AppUser>, IOtpEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlunkEmailSender> _logger;
    private readonly string _fromEmail;

    public PlunkEmailSender(HttpClient httpClient, ILogger<PlunkEmailSender> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fromEmail = configuration["PLUNK_FROM_EMAIL"] ?? "noreply@fasolt.app";
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
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

    public Task SendVerificationCodeAsync(AppUser user, string email, string code)
    {
        var body = $"""
            <p>Welcome to Fasolt!</p>
            <p>Your verification code is: <strong>{code}</strong></p>
            <p>It expires in 15 minutes. If you didn't request this, you can safely ignore this email — no account was created.</p>
            """;

        return SendAsync(email, "Your Fasolt verification code", body);
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
}
