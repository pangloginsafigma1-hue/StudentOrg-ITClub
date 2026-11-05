namespace ITClubOrg.Models;

public sealed class DivisionDropdownViewModel
{
    public required string InputName { get; init; }

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public IReadOnlyList<int> SelectedDivisionIds { get; init; } = [];

    public string? ValidationName { get; init; }
}
