using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetAllAsync(string search, int? divisionId, CancellationToken cancellationToken);

    Task<Activity?> GetByIdAsync(int activityId, int? divisionId, CancellationToken cancellationToken);

    Task SaveAsync(Activity activity, int? divisionId, CancellationToken cancellationToken);

    Task DeleteAsync(int activityId, int? divisionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AttendanceActivityOption>> GetAttendanceActivitiesAsync(
        int? divisionId,
        bool includeRecorded,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityRegistration>> GetRegistrationsAsync(int activityId, int? divisionId, CancellationToken cancellationToken);

    Task<bool> SetParticipationAsync(
        int activityId,
        string memberId,
        bool participated,
        string participationType,
        string? notes,
        int recordedBy,
        int? divisionId,
        CancellationToken cancellationToken);
}
