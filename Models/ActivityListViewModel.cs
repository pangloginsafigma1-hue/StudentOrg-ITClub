namespace ITClubOrg.Models;

public sealed class ActivityListViewModel
{
    public string Search { get; init; } = string.Empty;

    public IReadOnlyList<Activity> Activities { get; init; } = [];

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public bool CanChooseDivision { get; init; }

    public ActivityFormViewModel Form { get; init; } = new();

    public bool OpenForm { get; init; }

    public string? DatabaseError { get; init; }
}
