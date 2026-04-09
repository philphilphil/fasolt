using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Tests.Auth;

public class OAuthLoginPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthLoginPageTests(WebApplicationFactory<Program> factory)
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
    public async Task Get_Anonymous_RendersLoginForm()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/login?returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
        body.Should().Contain("action=\"/oauth/login\"");
        body.Should().Contain("name=\"Input.Email\"");
        body.Should().Contain("name=\"Input.Password\"");
        body.Should().Contain("Sign in to fasolt");
        body.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task Get_WithProviderHintGithub_AndGitHubConfigured_RedirectsToGitHubLogin()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GITHUB_CLIENT_ID"] = "test-client-id",
                    ["GITHUB_CLIENT_SECRET"] = "test-client-secret",
                });
            });
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/oauth/login?provider_hint=github&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/api/account/github-login");
    }

    [Theory]
    [InlineData("github_auth_failed", "GitHub authentication failed. Please try again.")]
    [InlineData("account_creation_failed", "Could not create your account. Please try again.")]
    public async Task Get_WithErrorQuery_RendersMappedMessage(string errorCode, string expectedMessage)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/oauth/login?returnUrl=%2F&error={errorCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(expectedMessage);
        body.Should().Contain("oauth-error");
    }

    [Fact]
    public async Task Post_MissingCsrf_Returns400()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "nobody@example.com",
            ["Input.Password"] = "Abcdefg1",
        });

        var response = await client.PostAsync("/oauth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_InvalidPassword_RendersFormWithError()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, "Abcdefg1")).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = "wrong-password",
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public async Task Post_ValidCredentials_RedirectsToReturnUrlAndSetsCookie()
    {
        var email = $"ok-{Guid.NewGuid():N}@example.com";
        const string password = "Abcdefg1";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2Fstudy");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "/study",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/study");
        response.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        setCookies!.Any(c => c.Contains(".AspNetCore.Identity.Application")).Should().BeTrue(
            "successful login must issue the Identity application cookie");
        setCookies!.Any(c => c.Contains(".AspNetCore.Identity.Application") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue("isPersistent: true must issue a cookie with an explicit Expires attribute, not a session cookie");
    }

    [Fact]
    public async Task Post_MaliciousReturnUrl_RedirectsToRootNotEvilDomain()
    {
        var email = $"redirect-{Guid.NewGuid():N}@example.com";
        const string password = "Abcdefg1";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=https%3A%2F%2Fevil.com");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "https://evil.com",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/",
            "UrlHelpers.IsLocalUrl must reject absolute URLs and fall back to '/'");
    }

    [Fact]
    public async Task Post_UnverifiedUser_RedirectsToVerifyEmail()
    {
        var email = $"unverified-{Guid.NewGuid():N}@example.com";
        const string password = "Abcdefg1";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = false };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/verify-email");
        response.Headers.Location!.OriginalString.Should().Contain(Uri.EscapeDataString(email));
    }

    [Fact]
    public async Task Post_InvalidEmailFormat_RendersFieldLevelError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = "not-an-email",
            ["Input.Password"] = "Abcdefg1",
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Data-annotation EmailAddress validator surfaces via ModelState into the
        // oauth-field-error span rendered by asp-validation-for. The regex requires
        // non-empty content inside the span, so a bare <span class="oauth-field-error"></span>
        // would fail — catching a broken asp-validation-for tag helper.
        body.Should().MatchRegex(@"class=""oauth-field-error[^""]*""[^>]*>\s*[^\s<][^<]*</span>");
    }

    private static string ExtractCsrfToken(string html)
    {
        // Try the pattern where name= and value= are adjacent (hand-rolled HTML)
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var idx = html.IndexOf(marker);
        if (idx >= 0)
        {
            var start = idx + marker.Length;
            var end = html.IndexOf("\"", start);
            return html.Substring(start, end - start);
        }

        // Razor Pages emits: <input name="__RequestVerificationToken" type="hidden" value="..."
        // Use a regex to find the value regardless of attribute order.
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        if (!match.Success)
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        if (match.Success) return match.Groups[1].Value;

        throw new InvalidOperationException("CSRF token not found in:\n" + html);
    }
}
