using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class Logout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/logout", async ([FromServices] SignInManager<IdentityUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok();
        })
        .Produces(StatusCodes.Status200OK)
        .WithTags("Identity")
        .RequireAuthorization()
        .WithOpenApi();
    }
}