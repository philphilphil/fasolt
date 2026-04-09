using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthForgotPasswordEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthForgotPasswordEndpointTests(WebApplicationFactory<Program> factory)
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
    public async Task Get_RendersEmailForm()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
        body.Should().Contain("name=\"Input.Email\"");
        body.Should().Contain("action=\"/oauth/forgot-password\"");
        body.Should().Contain("Reset your password");
    }

    [Fact]
    public async Task Get_WithSent_RendersCheckYourEmailConfirmation()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F&email=foo%40example.com&sent=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Check your email");
        body.Should().Contain("foo@example.com");
        // Must not leak whether the account actually exists.
        body.Should().NotContain("account does not exist", "generic confirmation only");
    }

    [Fact]
    public async Task Post_ForExistingConfirmedUser_CreatesResetCode_AndRedirectsToSent()
    {
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, "Abcdefg1")).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/forgot-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/oauth/forgot-password");
        response.Headers.Location!.OriginalString.Should().Contain("sent=true");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otpRow = await db.PasswordResetCodes.FirstOrDefaultAsync(r => r.UserId == userId);
        otpRow.Should().NotBeNull("a password reset row must exist for the confirmed user");
    }

    [Fact]
    public async Task Post_ForNonExistentUser_StillRedirectsToSent_AndCreatesNoRow()
    {
        // Enumeration guard: the response for an unknown email must be
        // indistinguishable from the response for a known email, and no
        // PasswordResetCodes row must exist afterward.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var email = $"does-not-exist-{Guid.NewGuid():N}@example.com";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/forgot-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("sent=true");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var totalRows = await db.PasswordResetCodes.CountAsync(
            r => r.User.Email == email);
        totalRows.Should().Be(0, "no row should be created for an unknown email");
    }

    [Fact]
    public async Task Post_ForExternalProviderUser_CreatesNoRow()
    {
        // GitHub/Apple users have no password to reset. Must not create a
        // dangling OTP row and must not reveal that the account is external.
        var email = $"gh-{Guid.NewGuid():N}@users.noreply.github.com";
        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser
            {
                UserName = $"ghuser-{Guid.NewGuid():N}",
                Email = email,
                EmailConfirmed = true,
                ExternalProvider = "GitHub",
                ExternalProviderId = Guid.NewGuid().ToString(),
            };
            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/forgot-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);
        response.Headers.Location!.OriginalString.Should().Contain("sent=true");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otpRow = await db.PasswordResetCodes.FirstOrDefaultAsync(r => r.UserId == userId);
        otpRow.Should().BeNull("external-provider accounts must not receive password reset codes");
    }

    [Fact]
    public async Task Post_WithMalformedEmail_RendersFormWithError()
    {
        // Syntactically invalid email → Razor model validation rejects via
        // [EmailAddress] attribute, PageModel sets ErrorMessage and returns
        // Page(). Unlike the old endpoint (which redirected with ?error=),
        // the new flow re-renders the form inline. Not a security regression:
        // email format is knowable client-side without hitting the server,
        // so distinguishing "valid format" from "invalid format" leaks nothing
        // about account existence. This test pins the new behavior as intentional.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = "not-an-email",
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/forgot-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Reset your password", "form re-renders on validation failure");
        body.Should().Contain("Please enter a valid email address.", "banner message is surfaced");
    }

    [Fact]
    public async Task Get_WithErrorQuery_RendersErrorBanner()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/forgot-password?returnUrl=%2F&error=Rate%20limit%20exceeded");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Rate limit exceeded");
        body.Should().Contain("oauth-error");
    }

    [Fact]
    public async Task LegacyForgotPasswordPath_RedirectsToOAuthForgotPassword()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/forgot-password?returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/forgot-password");
    }

    private static string ExtractCsrfToken(string html)
    {
        // Razor emits <input name="__RequestVerificationToken" type="hidden" value="...">
        // with attributes in varying order, so match name=...value=... flexibly.
        var regex = new System.Text.RegularExpressions.Regex(
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""|value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        var match = regex.Match(html);
        if (!match.Success) throw new InvalidOperationException("CSRF token not found in HTML");
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }
}
