using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthResetPasswordEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthResetPasswordEndpointTests(WebApplicationFactory<Program> factory)
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
    public async Task Get_RendersResetForm_WithEmailAndCodeInput()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/reset-password?email=foo@example.com&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("foo@example.com");
        body.Should().Contain("autocomplete=\"one-time-code\"");
        body.Should().Contain("name=\"code\"");
        body.Should().Contain("name=\"password\"");
        body.Should().Contain("name=\"confirmPassword\"");
    }

    [Fact]
    public async Task Post_WithCorrectCode_RotatesPassword_AndConsumesOtp()
    {
        var email = $"reset-ok-{Guid.NewGuid():N}@example.com";
        const string oldPassword = "OldPass1A";
        const string newPassword = "NewPass1B";
        string userId;
        string code;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, oldPassword)).Succeeded.Should().BeTrue();
            userId = user.Id;

            var otp = scope.ServiceProvider.GetRequiredService<IPasswordResetCodeService>();
            code = await otp.GenerateAndStoreAsync(user.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync($"/oauth/reset-password?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = code,
            ["password"] = newPassword,
            ["confirmPassword"] = newPassword,
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/reset-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Password updated");

        using var verifyScope = _factory.Services.CreateScope();
        var userManagerPost = verifyScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        var reloaded = await userManagerPost.FindByIdAsync(userId);
        reloaded.Should().NotBeNull();
        (await userManagerPost.CheckPasswordAsync(reloaded!, newPassword)).Should().BeTrue(
            "the new password should be accepted after reset");
        (await userManagerPost.CheckPasswordAsync(reloaded!, oldPassword)).Should().BeFalse(
            "the old password should no longer work");

        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otpRow = await db.PasswordResetCodes.FirstOrDefaultAsync(r => r.UserId == userId);
        otpRow.Should().BeNull("the OTP row must be consumed on successful reset");
    }

    [Fact]
    public async Task Post_WithWrongCode_ShowsError_AndDoesNotRotatePassword()
    {
        var email = $"reset-wrong-{Guid.NewGuid():N}@example.com";
        const string oldPassword = "OldPass1A";
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, oldPassword)).Succeeded.Should().BeTrue();
            userId = user.Id;

            var otp = scope.ServiceProvider.GetRequiredService<IPasswordResetCodeService>();
            await otp.GenerateAndStoreAsync(user.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync($"/oauth/reset-password?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = "000001",
            ["password"] = "NewPass1B",
            ["confirmPassword"] = "NewPass1B",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/reset-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/oauth/reset-password");
        response.Headers.Location!.OriginalString.Should().Contain("error=");
        response.Headers.Location!.OriginalString.Should().Contain("Incorrect");

        using var verifyScope = _factory.Services.CreateScope();
        var userManagerPost = verifyScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        var reloaded = await userManagerPost.FindByIdAsync(userId);
        (await userManagerPost.CheckPasswordAsync(reloaded!, oldPassword)).Should().BeTrue(
            "a failed code must not rotate the password");
    }

    [Fact]
    public async Task Post_WithMismatchedPasswords_ShowsError()
    {
        var email = $"reset-mismatch-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, "Abcdefg1");
            var otp = scope.ServiceProvider.GetRequiredService<IPasswordResetCodeService>();
            await otp.GenerateAndStoreAsync(user.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync($"/oauth/reset-password?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = "123456",
            ["password"] = "NewPass1A",
            ["confirmPassword"] = "DifferentPass1B",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/reset-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // The error flash gets percent-encoded, so the apostrophe in "don't" becomes %27.
        response.Headers.Location!.OriginalString.Should().Contain("Passwords%20don");
    }

    [Fact]
    public async Task Post_RejectsExternalProviderUser_EvenWithValidCode()
    {
        // Defence in depth: the forgot-password endpoint already blocks
        // external-provider users from ever getting a code, but if one
        // somehow arrives (prior account conversion, test fixture, etc.)
        // reset-password must still refuse to rotate the password.
        var email = $"gh-reset-{Guid.NewGuid():N}@users.noreply.github.com";
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

        // Smuggle a code directly into the table to simulate the worst case.
        using (var scope = _factory.Services.CreateScope())
        {
            var otp = scope.ServiceProvider.GetRequiredService<IPasswordResetCodeService>();
            await otp.GenerateAndStoreAsync(userId, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync($"/oauth/reset-password?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = "123456",
            ["password"] = "NewPass1A",
            ["confirmPassword"] = "NewPass1A",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/reset-password") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("error=");
    }

    [Fact]
    public async Task LegacyResetPasswordPath_RedirectsToOAuthResetPassword()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/reset-password?email=foo@example.com&token=abc");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/reset-password");
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
