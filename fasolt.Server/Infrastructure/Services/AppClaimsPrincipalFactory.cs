using System.Security.Claims;
using Fasolt.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Fasolt.Server.Infrastructure.Services;

public class AppClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<AppUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("email_confirmed", user.EmailConfirmed.ToString().ToLower()));
        return identity;
    }
}
