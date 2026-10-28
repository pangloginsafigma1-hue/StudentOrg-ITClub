namespace ITClubOrg.Models;

public sealed class AttendanceMember
{
    public string MemberId { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public bool IsPresent { get; init; }
}
