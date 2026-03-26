using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class AdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminUserListResponse> ListUsers(int page, int pageSize)
    {
        var totalCount = await _db.Users.CountAsync();

        var users = await _db.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email!,
                _db.Cards.Count(c => c.UserId == u.Id),
                _db.Decks.Count(d => d.UserId == u.Id),
                u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow,
                _db.DeviceTokens.Any(d => d.UserId == u.Id)))
            .ToListAsync();

        return new AdminUserListResponse(users, totalCount, page, pageSize);
    }
}
