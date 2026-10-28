using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IMemberRepository
{
    Task<IReadOnlyList<Member>> GetAllAsync(string search, int? divisionId, CancellationToken cancellationToken);

    Task<Member?> GetByIdAsync(string memberId, int? divisionId, CancellationToken cancellationToken);

    Task AddAsync(Member member, int? divisionId, CancellationToken cancellationToken);

    Task UpdateAsync(Member member, int? divisionId, CancellationToken cancellationToken);

    Task DeleteAsync(string memberId, int? divisionId, CancellationToken cancellationToken);
}
