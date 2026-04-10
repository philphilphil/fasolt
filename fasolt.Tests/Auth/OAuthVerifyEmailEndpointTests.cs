using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

[Collection(WebAppCollection.Name)]
public class OAuthVerifyEmailEndpointTests
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
        // Razor generates name="Input.Code" for asp-for="Input.Code"
        body.Should().Contain("name=\"Input.Code\"");
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

        var getResponse = await client.GetAsync($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        // Razor Pages bind Email and ReturnUrl from top-level BindProperty,
        // and Input.Code from the InputModel — names match the generated hidden inputs.
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Email"] = email,
            ["Input.Code"] = code,
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/");

        // Session should be persistent (isPersistent: true) — cookie has an expires attribute.
        // This assertion guards against a silent regression of the Task 4 convergence
        // decision to default to persistent sessions across all auth pages.
        response.Headers.Contains("Set-Cookie").Should().BeTrue();
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        var identityCookie = setCookieHeaders.FirstOrDefault(c => c.Contains(".AspNetCore.Identity.Application"));
        identityCookie.Should().NotBeNull("successful verify must issue the Identity cookie on the POST response");
        identityCookie!.Should().Contain("expires=",
            "isPersistent: true must issue a cookie with an explicit Expires attribute, not a session cookie");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.EmailConfirmed.Should().BeTrue();
        var otpRow = await db.EmailVerificationCodes.FirstOrDefaultAsync(r => r.UserId == userId);
        otpRow.Should().BeNull();
    }

    [Fact]
    public async Task Post_WithWrongCode_ShowsErrorInline()
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
        var getResponse = await client.GetAsync($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Email"] = email,
            ["Input.Code"] = "000001",
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        // Razor page returns 200 with inline error — no redirect
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Incorrect code, try again.");
    }

    [Fact]
    public async Task PostResend_UnknownEmail_RedirectsCleanly()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // GET to grab a CSRF token
        var getResponse = await client.GetAsync("/oauth/verify-email?email=ghost@example.com&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Email"] = "ghost@example.com",
            ["ReturnUrl"] = "/",
        });
        // Resend handler is reached via ?handler=resend (named Razor Page handler)
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email?handler=resend") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        // Unknown user → clean redirect to /oauth/verify-email (no error param)
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().Contain("/oauth/verify-email");
        location.Should().NotContain("error=");
    }

    private static string ExtractCsrfToken(string html)
    {
        var regex = new System.Text.RegularExpressions.Regex(
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""|value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        var match = regex.Match(html);
        if (!match.Success) throw new InvalidOperationException("CSRF token not found");
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }
}
