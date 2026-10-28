using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IDashboardRepository
{
    Task<DashboardData> GetDashboardDataAsync(int? divisionId, CancellationToken cancellationToken);
}
