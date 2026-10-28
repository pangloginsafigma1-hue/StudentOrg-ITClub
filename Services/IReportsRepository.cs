using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IReportsRepository
{
    Task<IReadOnlyList<ReportMemberOption>> GetMembersAsync(int? divisionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportActivityOption>> GetRecordedActivitiesAsync(
        int? divisionId,
        CancellationToken cancellationToken);

    Task<ReportTable> GetMembersReportAsync(
        string? memberId,
        string? position,
        string? status,
        int? divisionId,
        CancellationToken cancellationToken);

    Task<ReportTable> GetActivitiesReportAsync(
        DateTime fromDate,
        DateTime toDate,
        int? divisionId,
        CancellationToken cancellationToken);

    Task<ReportTable?> GetAttendanceReportAsync(int activityId, int? divisionId, CancellationToken cancellationToken);
}
