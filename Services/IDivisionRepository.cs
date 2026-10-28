using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IDivisionRepository
{
    Task<IReadOnlyList<Division>> GetAllAsync(CancellationToken cancellationToken);
}
