namespace ITClubOrg.Models;

public sealed class DashboardMember
{
    public string MemberId { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string Course { get; init; } = string.Empty;

    public DateTime DateJoined { get; init; }

    public string? DivisionName { get; init; }
}
