namespace ITClubOrg.Models;

public sealed class AdminReserveNomination
{
    public string SlotKey { get; init; } = string.Empty;

    public string SlotName { get; init; } = string.Empty;

    public string? MemberId { get; init; }

    public string? MemberName { get; init; }

    public IReadOnlyList<AdminReserveCandidate> CandidateOptions { get; init; } = [];
}
