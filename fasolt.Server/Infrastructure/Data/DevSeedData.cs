using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Api.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Data;

public static class DevSeedData
{
    // Pre-generated token for local development / MCP testing.
    // Token: sm_dev_token_for_local_testing_only_do_not_use_in_production_0000
    public const string DevToken = "sm_dev_token_for_local_testing_only_do_not_use_in_production_0000";
    public const string DevEmail = "dev@fasolt.local";
    public const string DevPassword = "Dev1234!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await userManager.FindByEmailAsync(DevEmail);
        if (existing is not null) return;

        var user = new AppUser
        {
            UserName = DevEmail,
            Email = DevEmail,
            DisplayName = "Dev User",
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, DevPassword);
        if (!result.Succeeded) return;

        var hash = BearerTokenHandler.ComputeHash(DevToken);
        db.ApiTokens.Add(new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Dev Token (auto-seeded)",
            TokenHash = hash,
            TokenPrefix = DevToken[..8],
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
    }
}
