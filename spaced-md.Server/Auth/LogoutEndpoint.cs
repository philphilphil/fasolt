using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class Logout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/logout", async (SignInManager<IdentityUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok();
        })
        .WithTags("Identity")
        .RequireAuthorization()
        .WithOpenApi();
    }
}