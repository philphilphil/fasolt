using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public sealed class LoginEndpoint : IEndpoint
{
    public record LoginRequest(string Email, string Password, bool RememberMe);
    public record LoginResponse(bool success, string Message);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/login", Handle)
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .WithOpenApi()
        .WithTags("Identity");
    }

    static async Task<IResult> Handle(LoginRequest request,
            [FromServices] SignInManager<IdentityUser> signInManager,
            [FromServices] UserManager<IdentityUser> userManager
           )
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null) return Results.Ok(new LoginResponse(false, "Invalid login."));

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: request.RememberMe, lockoutOnFailure: false);

        return result.Succeeded
            ? Results.Ok(new LoginResponse(true, "Login successful."))
            : Results.Ok(new LoginResponse(false, "Invalid login."));
    }

}