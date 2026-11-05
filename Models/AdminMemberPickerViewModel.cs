namespace ITClubOrg.Models;

public sealed class AdminMemberPickerViewModel
{
    public string Search { get; init; } = string.Empty;

    public IReadOnlyList<AdminMemberCandidate> Candidates { get; init; } = [];

    public bool CanCreateSuperAdmin { get; init; } = true;

    public string? DatabaseError { get; init; }
}
