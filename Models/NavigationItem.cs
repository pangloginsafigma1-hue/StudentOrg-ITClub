namespace ITClubOrg.Models;

public sealed class NavigationItem
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? IconPath { get; init; }
}
