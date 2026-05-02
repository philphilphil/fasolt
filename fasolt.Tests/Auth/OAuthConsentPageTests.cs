using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Fasolt.Server.Domain.Entities;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

[Collection(WebAppCollection.Name)]
public class OAuthConsentPageTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthConsentPageTests(WebApplicationFactory<Program> factory)
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
    public async Task Get_WithSnakeCaseClientId_RendersConsentPage()
    {
        var email = $"consent-{Guid.NewGuid():N}@example.com";
        const string password = "Abcdefg1";
        var clientId = $"test-client-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();

            var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await appManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "Claude Code",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
            });
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var authCookie = await LoginAndGetAuthCookie(client, email, password);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/oauth/consent?client_id={Uri.EscapeDataString(clientId)}");
        request.Headers.Add("Cookie", authCookie);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Claude Code");
    }

    private static async Task<string> LoginAndGetAuthCookie(HttpClient client, string email, string password)
    {
        var getResponse = await client.GetAsync("/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "/",
        });

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/login") { Content = loginContent };
        loginRequest.Headers.Add("Cookie", cookieHeader);
        var loginResponse = await client.SendAsync(loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return loginResponse.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal));
    }

    private static string ExtractCsrfToken(string html)
    {
        var match = Regex.Match(html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""|value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");

        match.Success.Should().BeTrue("login page should include an antiforgery token");
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }
}
