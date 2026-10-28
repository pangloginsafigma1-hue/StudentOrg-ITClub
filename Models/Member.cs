namespace ITClubOrg.Models;

public sealed class Member
{
    public string MemberId { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string Course { get; init; } = string.Empty;

    public string YearLevel { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime DateJoined { get; init; }

    public int? DivisionId { get; init; }

    public string? DivisionName { get; init; }
}
