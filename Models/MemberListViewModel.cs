namespace ITClubOrg.Models;

public sealed class MemberListViewModel
{
    public string Search { get; init; } = string.Empty;

    public IReadOnlyList<Member> Members { get; init; } = [];

    public string? DatabaseError { get; init; }
}
