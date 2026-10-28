namespace ITClubOrg.Models;

public sealed class PasswordResetAccount
{
    public int AdminId { get; init; }

    public string Email { get; init; } = string.Empty;
}
