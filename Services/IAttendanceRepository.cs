using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IAttendanceRepository
{
    Task<IReadOnlyList<AttendanceMember>> GetRosterAsync(
        int activityId,
        int? divisionId,
        CancellationToken cancellationToken);

    Task<bool> SaveAsync(
        int activityId,
        IReadOnlyCollection<string> presentMemberIds,
        int? divisionId,
        CancellationToken cancellationToken);
}
