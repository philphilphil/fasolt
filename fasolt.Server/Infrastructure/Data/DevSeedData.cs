using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Data;

public static class DevSeedData
{
    public const string DevEmail = "dev@fasolt.local";
    public const string DevPassword = "Dev1234!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var existing = await userManager.FindByEmailAsync(DevEmail);
        if (existing is not null)
        {
            // Ensure dev user has Admin role even if already created
            if (!await userManager.IsInRoleAsync(existing, "Admin"))
                await userManager.AddToRoleAsync(existing, "Admin");
            return;
        }

        var user = new AppUser
        {
            UserName = DevEmail,
            Email = DevEmail,
            DisplayName = "Dev User",
            EmailConfirmed = true,
        };

        await userManager.CreateAsync(user, DevPassword);
        await userManager.AddToRoleAsync(user, "Admin");
    }
}
