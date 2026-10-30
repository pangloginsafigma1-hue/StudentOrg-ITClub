namespace ITClubOrg.Models;

public sealed class AttendanceActivityOption
{
    public int ActivityId { get; init; }

    public string ActivityName { get; init; } = string.Empty;

    public DateTime ActivityDate { get; init; }

    public DateTime ActivityEndDate { get; init; }

    public bool AttendanceDone { get; init; }

    public string? DivisionName { get; init; }
}
