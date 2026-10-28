namespace ITClubOrg.Models;

public sealed class AdminAccount
{
    public int AdminId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public int? DivisionId { get; init; }

    public string? DivisionName { get; init; }
}
