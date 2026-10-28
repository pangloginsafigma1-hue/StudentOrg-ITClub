using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IAdminAuthService
{
    Task<AdminAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);

    Task<bool> IsAuthorizationCurrentAsync(
        int adminId,
        string role,
        int? divisionId,
        CancellationToken cancellationToken);

    Task<bool> PrepareDatabaseAsync(CancellationToken cancellationToken);
}
