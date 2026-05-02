using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Pages.Oauth;

[AllowAnonymous]
[ValidateAntiForgeryToken]
public class ConsentModel : PageModel
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly AppDbContext _db;

    public ConsentModel(
        IOpenIddictApplicationManager applicationManager,
        IDataProtectionProvider dataProtection,
        AppDbContext db)
    {
        _applicationManager = applicationManager;
        _dataProtection = dataProtection;
        _db = db;
    }

    [BindProperty(SupportsGet = true, Name = "client_id")]
    public string ClientId { get; set; } = "";

    public string ClientName { get; set; } = "";

    [BindProperty]
    public string Decision { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result?.Principal is null)
            return Redirect("/login");

        if (string.IsNullOrWhiteSpace(ClientId))
            return BadRequest("Missing client_id");

        var application = await _applicationManager.FindByClientIdAsync(ClientId);
        ClientName = application is not null
            ? (await _applicationManager.GetDisplayNameAsync(application) ?? ClientId)
            : ClientId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (authResult?.Principal is null)
            return Redirect("/login");

        var userId = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (string.IsNullOrWhiteSpace(ClientId))
            return BadRequest("Missing client_id");

        var application = await _applicationManager.FindByClientIdAsync(ClientId);
        if (application is null)
            return BadRequest("Unknown client");

        // Validate that an active OAuth flow exists (cookie must be present)
        var encryptedQuery = Request.Cookies["oauth_authorize_query"];
        if (string.IsNullOrEmpty(encryptedQuery))
            return BadRequest("No active authorization flow");

        // Decrypt and validate the stored query string
        var protector = _dataProtection.CreateProtector("OAuthAuthorizeQuery");
        string authorizeQuery;
        try
        {
            authorizeQuery = protector.Unprotect(encryptedQuery);
        }
        catch
        {
            return BadRequest("Invalid or expired authorization flow");
        }

        // Verify the client_id in the cookie matches the consent form
        var queryParams = QueryHelpers.ParseQuery(authorizeQuery.TrimStart('?'));
        if (!queryParams.TryGetValue("client_id", out var cookieClientId) || cookieClientId != ClientId)
            return BadRequest("Client ID mismatch");

        Response.Cookies.Delete("oauth_authorize_query");

        if (Decision == "approve")
        {
            // Store consent grant
            var existing = await _db.ConsentGrants
                .FirstOrDefaultAsync(g => g.UserId == userId && g.ClientId == ClientId);
            if (existing is null)
            {
                _db.ConsentGrants.Add(new ConsentGrant
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ClientId = ClientId,
                    GrantedAt = DateTimeOffset.UtcNow,
                });
                await _db.SaveChangesAsync();
            }

            // Redirect back to authorize endpoint
            return Redirect($"/oauth/authorize{authorizeQuery}");
        }
        else
        {
            // Deny — redirect to client with error
            var redirectUris = await _applicationManager.GetRedirectUrisAsync(application);
            var clientRedirectUri = redirectUris.FirstOrDefault() ?? "/";
            var separator = clientRedirectUri.Contains('?') ? '&' : '?';
            return Redirect($"{clientRedirectUri}{separator}error=access_denied");
        }
    }
}
