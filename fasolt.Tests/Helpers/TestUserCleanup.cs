using Fasolt.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Fasolt.Tests.Helpers;

public static class TestUserCleanup
{
    public static async Task DeleteTestUsersAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const string pattern = "^test-[a-f0-9]{32}@";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"AspNetUsers\" WHERE \"Email\" ~ {pattern}");
    }
}
