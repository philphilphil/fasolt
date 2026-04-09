using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthVerifyEmailEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthVerifyEmailEndpointTests(WebApplicationFactory<Program> factory)
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
    public async Task Get_RendersVerifyForm_WithEmailPrefilled()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/verify-email?email=foo@example.com&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("foo@example.com");
        body.Should().Contain("autocomplete=\"one-time-code\"");
        body.Should().Contain("name=\"code\"");
    }

    [Fact]
    public async Task Post_WithCorrectCode_ConfirmsEmail_AndRedirectsToReturnUrl()
    {
        var email = $"verify-{Guid.NewGuid():N}@example.com";
        string code;
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var newUser = new AppUser { UserName = email, Email = email };
            await userManager.CreateAsync(newUser, "Abcdefg1");
            userId = newUser.Id;
            var otp = scope.ServiceProvider.GetRequiredService<IEmailVerificationCodeService>();
            code = await otp.GenerateAndStoreAsync(newUser.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync($"/oauth/verify-email?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = code,
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.EmailConfirmed.Should().BeTrue();
        var otpRow = await db.EmailVerificationCodes.FirstOrDefaultAsync(r => r.UserId == userId);
        otpRow.Should().BeNull();
    }

    [Fact]
    public async Task Post_WithWrongCode_ShowsError()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email };
            await userManager.CreateAsync(user, "Abcdefg1");
            var otp = scope.ServiceProvider.GetRequiredService<IEmailVerificationCodeService>();
            await otp.GenerateAndStoreAsync(user.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync($"/oauth/verify-email?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = "000001",
            ["returnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().Contain("/oauth/verify-email");
        location.Should().Contain("error=");
        location.Should().Contain("Incorrect");
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
