using System.Security.Claims;
using Fasolt.Server.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Fasolt.Server.Infrastructure.Services;

public class TrackingSignInManager(
    UserManager<AppUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<AppUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<AppUser> confirmation)
    : SignInManager<AppUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task SignInWithClaimsAsync(
        AppUser user,
        AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await UserManager.UpdateAsync(user);
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
    }
}
