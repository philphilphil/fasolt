using System.Security.Claims;
using Fasolt.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Fasolt.Server.Infrastructure.Services;

public class AppClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("email_confirmed", user.EmailConfirmed.ToString().ToLower()));
        if (user.ExternalProvider is not null)
            identity.AddClaim(new Claim("external_provider", user.ExternalProvider));
        return identity;
    }
}
