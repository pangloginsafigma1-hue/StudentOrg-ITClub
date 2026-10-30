using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IReportsRepository
{
    Task<IReadOnlyList<ReportMemberOption>> GetMembersAsync(IReadOnlyList<int> divisionIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportActivityOption>> GetRecordedActivitiesAsync(
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken);

    Task<ReportTable> GetMembersReportAsync(
        string? memberId,
        string? position,
        string? status,
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken);

    Task<ReportTable> GetActivitiesReportAsync(
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken);

    Task<ReportTable?> GetAttendanceReportAsync(int activityId, IReadOnlyList<int> divisionIds, CancellationToken cancellationToken);
}
