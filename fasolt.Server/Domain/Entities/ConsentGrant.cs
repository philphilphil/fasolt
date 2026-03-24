namespace Fasolt.Server.Domain.Entities;

public class ConsentGrant
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public DateTimeOffset GrantedAt { get; set; }
}
