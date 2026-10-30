using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IAdminAccountRepository
{
    Task<IReadOnlyList<AdminAccountListItem>> GetAllAsync(CancellationToken cancellationToken);

    Task<AdminAccountFormViewModel?> GetByIdAsync(int adminId, CancellationToken cancellationToken);

    Task RecordPositionTermAsync(int adminId, string position, DateTime termStart, DateTime? termEnd, CancellationToken cancellationToken);

    Task<bool> IsUsernameTakenAsync(string username, int exceptAdminId, CancellationToken cancellationToken);

    Task CreateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken);

    Task UpdateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken);
}
