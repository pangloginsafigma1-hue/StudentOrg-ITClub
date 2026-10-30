using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IMemberPortalRepository
{
    Task<bool> RegisterAsync(MemberRegistrationViewModel model, CancellationToken cancellationToken);

    Task<Member?> AuthenticateAsync(string memberId, string password, CancellationToken cancellationToken);

    Task<MemberPasswordResetAccount?> GetMemberPasswordResetAccountByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<bool> ResetMemberPasswordAsync(
        string memberId,
        string newPassword,
        CancellationToken cancellationToken);

    Task<bool> IsAuthorizationCurrentAsync(string memberId, CancellationToken cancellationToken);

    Task<MemberPortalViewModel> GetPortalAsync(string memberId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberStatusHistory>> GetStatusHistoryAsync(string memberId, CancellationToken cancellationToken);

    Task<bool> RegisterForActivityAsync(string memberId, int activityId, CancellationToken cancellationToken);

    Task<bool> UnregisterFromActivityAsync(string memberId, int activityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberPortalAccountListItem>> GetPortalAccountsAsync(CancellationToken cancellationToken);

    Task<bool> SetPortalAccountActiveAsync(
        string memberId,
        bool isActive,
        CancellationToken cancellationToken);

    Task<bool> ResetPortalPasswordAsync(
        string memberId,
        string newPassword,
        CancellationToken cancellationToken);

    Task<bool> ChangePortalPasswordAsync(
        string memberId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberAnnouncement>> GetAnnouncementsAsync(
        int? divisionId,
        bool includeAllDivisions,
        CancellationToken cancellationToken);

    Task CreateAnnouncementAsync(
        string title,
        string body,
        IReadOnlyList<int> divisionIds,
        int publishedBy,
        CancellationToken cancellationToken);

    Task<bool> DeleteAnnouncementAsync(
        int announcementId,
        IReadOnlyList<int> divisionIds,
        bool includeAllDivisions,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberRegistrationRequest>> GetPendingRegistrationsAsync(
        int? divisionId,
        CancellationToken cancellationToken);

    Task<RegistrationApprovalResult> ApproveRegistrationAsync(
        string memberId,
        int? divisionId,
        int reviewedBy,
        CancellationToken cancellationToken);

    Task<bool> RejectRegistrationAsync(
        string memberId,
        int? divisionId,
        int reviewedBy,
        CancellationToken cancellationToken);
}
