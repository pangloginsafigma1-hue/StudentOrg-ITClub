namespace ITClubOrg.Models;

public sealed class DashboardActivity
{
    public string ActivityName { get; init; } = string.Empty;

    public DateTime ActivityDate { get; init; }

    public string Location { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? DivisionName { get; init; }
}
