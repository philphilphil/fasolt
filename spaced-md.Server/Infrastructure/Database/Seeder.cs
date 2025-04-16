using Microsoft.AspNetCore.Identity;

public static class InitUserSeeder
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string adminRole = "Administrator";
        string userName = "local@spaced-md.com";
        string hardcodedPassword = "sp4cEd!"; // Hardcoded for local/dev only

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            user = new IdentityUser
            {
                UserName = userName,
                Email = userName,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, hardcodedPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, adminRole);
            }
            else
            {
                throw new Exception($"Failed to create seed user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
