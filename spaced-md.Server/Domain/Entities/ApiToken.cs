namespace SpacedMd.Server.Domain.Entities;

public class ApiToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public string TokenPrefix { get; set; } = default!; // first 8 chars for identification: "sm_XXXXX"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
