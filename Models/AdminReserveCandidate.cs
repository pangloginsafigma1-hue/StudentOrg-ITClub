namespace ITClubOrg.Models;

public sealed class AdminReserveCandidate
{
    public string MemberId { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string? DivisionName { get; init; }
}
