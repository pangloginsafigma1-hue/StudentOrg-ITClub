namespace ITClubOrg.Models;

public sealed class MemberListViewModel
{
    public string Search { get; init; } = string.Empty;

    public IReadOnlyList<Member> Members { get; init; } = [];

    public IReadOnlyList<MemberRegistrationRequest> PendingRegistrations { get; init; } = [];

    public string? DatabaseError { get; init; }
}
