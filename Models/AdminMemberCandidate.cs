namespace ITClubOrg.Models;

public sealed class AdminMemberCandidate
{
    public string MemberId { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public string Course { get; init; } = string.Empty;

    public string YearLevel { get; init; } = string.Empty;

    public DateTime DateJoined { get; init; }

    public int? DivisionId { get; init; }

    public string? DivisionName { get; init; }

    public string? Email { get; init; }

    public bool CanBeDivisionAdmin { get; init; }

    public bool CanBeSuperAdmin { get; init; }
}
