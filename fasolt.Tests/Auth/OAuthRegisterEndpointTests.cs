using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthRegisterEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthRegisterEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OTP_PEPPER"] = "test-pepper",
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Fact]
    public async Task Get_RendersRegisterForm()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/register?returnUrl=%2Foauth%2Fauthorize%3F...");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
        body.Should().Contain("name=\"email\"");
        body.Should().Contain("name=\"password\"");
        body.Should().Contain("name=\"confirmPassword\"");
        body.Should().Contain("name=\"tosAccepted\"");
        body.Should().Contain("Already have an account?");
    }

    [Fact]
    public async Task Post_WithValidFields_CreatesUnverifiedUser_AndRedirectsToVerify()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Prime CSRF by GETting the form first
        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var email = $"register-test-{Guid.NewGuid():N}@example.com";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["password"] = "Abcdefg1",
            ["confirmPassword"] = "Abcdefg1",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/verify-email");
        response.Headers.Location!.OriginalString.Should().Contain(Uri.EscapeDataString(email));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();

        var codeRow = await db.EmailVerificationCodes.FirstOrDefaultAsync(r => r.UserId == user.Id);
        codeRow.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_WithMismatchedPasswords_ReturnsFormWithError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = "mismatch@example.com",
            ["password"] = "Abcdefg1",
            ["confirmPassword"] = "Different2",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().Contain("/oauth/register");
        location.Should().Contain("error=");
        location.Should().Contain("don%27t%20match").And.NotContain("verify-email");
    }

    [Fact]
    public async Task Post_WhenUnconfirmedUserHasReachedSendCap_ReturnsError()
    {
        var email = $"cap-test-{Guid.NewGuid():N}@example.com";
        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = false };
            await userManager.CreateAsync(user, "Abcdefg1");
            userId = user.Id;

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Seed an OTP row at the session cap so the next generate would throw
            db.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                CodeHash = "dummy",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                Attempts = 0,
                SentCount = 5,
                LastSentAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                LockedUntil = null,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["password"] = "Abcdefg1",
            ["confirmPassword"] = "Abcdefg1",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().StartWith("/oauth/register");
        location.Should().Contain("error=");
        location.Should().Contain("Too");
    }

    [Fact]
    public async Task Post_WhenEmailAlreadyConfirmed_RedirectsWithError()
    {
        var email = $"confirmed-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, "Abcdefg1");
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["password"] = "Xyzwvut2",
            ["confirmPassword"] = "Xyzwvut2",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().StartWith("/oauth/register");
        location.Should().Contain("already");
    }

    [Fact]
    public async Task Post_WithWeakPassword_ReturnsFormWithError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var email = $"weak-{Guid.NewGuid():N}@example.com";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["password"] = "short", // too short, no digits, no uppercase
            ["confirmPassword"] = "short",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().StartWith("/oauth/register");
        location.Should().Contain("error=");

        // Verify no user was created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().BeNull();
    }

    private static string ExtractCsrfToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var idx = html.IndexOf(marker);
        if (idx < 0) throw new InvalidOperationException("CSRF token not found");
        var start = idx + marker.Length;
        var end = html.IndexOf("\"", start);
        return html.Substring(start, end - start);
    }
}
