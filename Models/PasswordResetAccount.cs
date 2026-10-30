namespace ITClubOrg.Models;

public sealed class PasswordResetAccount
{
    public int AdminId { get; init; }

    public string Email { get; init; } = string.Empty;
}

public sealed class MemberPasswordResetAccount
{
    public string MemberId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}
