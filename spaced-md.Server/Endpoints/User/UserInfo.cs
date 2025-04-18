using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public sealed class ManageInfoEndpoint : IEndpoint
{

    public record UserInfoResponse(string Email, bool IsEmailConfirmed);
    public record UserInfoUpdateRequest(string? NewEmail, string? NewPassword, string OldPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/user/info", GetInfo)
            .Produces<UserInfoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization()
            .WithOpenApi()
            .WithTags("User");

        app.MapPost("/user/info", UpdateInfo)
            .Produces<UserInfoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization()
            .WithOpenApi()
            .WithTags("User");
    }

    static async Task<IResult> GetInfo(
        UserManager<IdentityUser> userManager,
        ClaimsPrincipal user)
    {
        var identityUser = await userManager.GetUserAsync(user);
        return identityUser == null
            ? Results.Unauthorized()
            : Results.Ok(new UserInfoResponse(identityUser.Email!, identityUser.EmailConfirmed));
    }

    static async Task<IResult> UpdateInfo(
        UserManager<IdentityUser> userManager,
        [FromBody] UserInfoUpdateRequest request,
        ClaimsPrincipal user)
    {
        var identityUser = await userManager.GetUserAsync(user);
        if (identityUser == null) return Results.Unauthorized();

        var passwordValid = await userManager.CheckPasswordAsync(identityUser, request.OldPassword);
        if (!passwordValid) return Results.BadRequest("Invalid password");

        if (!string.IsNullOrEmpty(request.NewEmail))
            identityUser.Email = request.NewEmail;

        if (!string.IsNullOrEmpty(request.NewPassword))
            await userManager.ChangePasswordAsync(identityUser, request.OldPassword, request.NewPassword);

        await userManager.UpdateAsync(identityUser);

        return Results.Ok(new UserInfoResponse(identityUser.Email!, identityUser.EmailConfirmed));
    }

}