using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class UserRolesViewModel
{
    public IReadOnlyList<AdminAccountListItem> AdministratorAccounts { get; init; } = [];

    public IReadOnlyList<MemberPortalAccountListItem> MemberAccounts { get; init; } = [];

    public string? DatabaseError { get; init; }
}

public sealed class MemberPortalAccountListItem
{
    public string MemberId { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string? DivisionName { get; init; }

    public string MembershipStatus { get; init; } = string.Empty;

    public bool IsPortalAccountActive { get; init; }
}

public sealed class ResetMemberPasswordViewModel
{
    [Required]
    [StringLength(20)]
    public string MemberId { get; set; } = string.Empty;

    [Required]
    [StringLength(72, MinimumLength = 12)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
