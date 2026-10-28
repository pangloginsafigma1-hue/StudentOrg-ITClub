namespace ITClubOrg.Models;

public sealed class DashboardData
{
    public int MemberCount { get; init; }

    public int ActivityCount { get; init; }

    public int ActiveMemberCount { get; init; }

    public int NewMemberCount30Days { get; init; }

    public int UpcomingActivityCount { get; init; }

    public int AttendanceRecordedCount { get; init; }

    public IReadOnlyList<DashboardActivity> UpcomingActivities { get; init; } = [];

    public IReadOnlyList<DashboardActivity> RecentActivities { get; init; } = [];

    public IReadOnlyList<DashboardMember> RecentMembers { get; init; } = [];

    public IReadOnlyList<DashboardTrendPoint> MonthlyTrend { get; init; } = [];
}
