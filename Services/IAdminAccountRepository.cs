using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IAdminAccountRepository
{
    Task<IReadOnlyList<AdminAccountListItem>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminMemberCandidate>> GetActiveMemberCandidatesAsync(
        string search,
        CancellationToken cancellationToken);

    Task<AdminMemberCandidate?> GetActiveMemberCandidateAsync(
        string memberId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminReserveNomination>> GetReserveNominationsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminReserveCandidate>> GetReserveCandidatesAsync(
        string slotKey,
        CancellationToken cancellationToken);

    Task SaveReserveNominationAsync(
        string slotKey,
        string? memberId,
        int updatedBy,
        CancellationToken cancellationToken);

    Task<AdminAccountFormViewModel?> GetByIdAsync(int adminId, CancellationToken cancellationToken);

    Task<int> GetSuperAdminCountAsync(CancellationToken cancellationToken);

    Task RecordPositionTermAsync(int adminId, string position, DateTime termStart, DateTime? termEnd, CancellationToken cancellationToken);

    Task<bool> IsUsernameTakenAsync(string username, int exceptAdminId, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetAssignedDivisionIdsAsync(int exceptAdminId, CancellationToken cancellationToken);

    Task CreateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken);

    Task UpdateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken);
}
