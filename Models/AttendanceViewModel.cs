namespace ITClubOrg.Models;

public sealed class AttendanceViewModel
{
    public IReadOnlyList<AttendanceActivityOption> Activities { get; init; } = [];

    public int? SelectedActivityId { get; init; }

    public string SelectedActivityName { get; init; } = string.Empty;

    public IReadOnlyList<AttendanceMember> Members { get; init; } = [];

    public string? DatabaseError { get; init; }

    public bool HasActivity => SelectedActivityId.HasValue;
}
