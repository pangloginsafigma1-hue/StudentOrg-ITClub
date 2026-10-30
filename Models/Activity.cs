namespace ITClubOrg.Models;

public sealed class Activity
{
    public int ActivityId { get; init; }

    public string ActivityName { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public DateTime ActivityDate { get; init; }

    public DateTime ActivityEndDate { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool AttendanceDone { get; init; }

    public int? DivisionId { get; init; }

    public string? DivisionName { get; init; }
}
