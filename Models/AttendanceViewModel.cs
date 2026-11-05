namespace ITClubOrg.Models;

public sealed class AttendanceViewModel
{
    public IReadOnlyList<AttendanceActivityOption> Activities { get; init; } = [];

    public int? SelectedActivityId { get; init; }

    public string SelectedActivityName { get; init; } = string.Empty;

    public bool SelectedActivityAttendanceDone { get; init; }

    public bool CanEditRecordedAttendance { get; init; }

    public int ActivityPage { get; init; } = 1;

    public int ActivityPageCount { get; init; }

    public int ActivityCount { get; init; }

    public string? NotStartedActivityName { get; init; }

    public IReadOnlyList<AttendanceMember> Members { get; init; } = [];

    public string? DatabaseError { get; init; }

    public bool HasActivity => SelectedActivityId.HasValue;
}
