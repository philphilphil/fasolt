using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Domain.Entities;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
