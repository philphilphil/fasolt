namespace Fasolt.Server.Domain.Entities;

/// <summary>
/// Per-user spaced-repetition state for a card. Created lazily on first review
/// (or first suspend) — the absence of a row means the card is "new" for that
/// user: not suspended, no due date, no FSRS parameters.
/// </summary>
public class ReviewState
{
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public Guid CardId { get; set; }
    public Card Card { get; set; } = default!;

    public double? Stability { get; set; }
    public double? Difficulty { get; set; }
    public int? Step { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string State { get; set; } = "new";
    public DateTimeOffset? LastReviewedAt { get; set; }
    public bool IsSuspended { get; set; } = false;

    /// <summary>
    /// True when this row carries no information beyond the implicit "new" default,
    /// i.e. it is equivalent to having no row at all.
    /// </summary>
    public bool IsPristine =>
        State == "new" && LastReviewedAt is null && !IsSuspended
        && Stability is null && Difficulty is null && Step is null && DueAt is null;
}
