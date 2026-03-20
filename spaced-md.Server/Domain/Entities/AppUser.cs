using Microsoft.AspNetCore.Identity;

namespace SpacedMd.Server.Domain.Entities;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
