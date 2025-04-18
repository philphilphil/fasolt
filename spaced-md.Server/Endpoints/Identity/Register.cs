using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public sealed class RegisterEndpoint : IEndpoint
{
    public record RegisterRequest(string Email, string Password);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/register", async (
            UserManager<IdentityUser> userManager,
            [FromBody] RegisterRequest request) =>
        {
            var user = new IdentityUser { UserName = request.Email, Email = request.Email };
            var result = await userManager.CreateAsync(user, request.Password);
            return result.Succeeded ? Results.Ok() : Results.BadRequest(result.Errors);
        })
        .Produces(StatusCodes.Status200OK)
        .Produces<IEnumerable<IdentityError>>(StatusCodes.Status400BadRequest)
        .WithOpenApi()
        .WithTags("Identity");
    }

}