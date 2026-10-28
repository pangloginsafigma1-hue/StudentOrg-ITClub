using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class DashboardRepository(MySqlConnectionFactory connectionFactory) : IDashboardRepository
{
    public async Task<DashboardData> GetDashboardDataAsync(int? divisionId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var today = DateTime.Today;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var trendStart = currentMonth.AddMonths(-11);
        var trendEnd = currentMonth.AddMonths(1);
        var memberCount = await GetCountAsync(
            connection,
            "SELECT COUNT(*) FROM members WHERE (@divisionId IS NULL OR division_id = @divisionId)",
            divisionId,
            null,
            cancellationToken);
        var activeMemberCount = await GetCountAsync(
            connection,
            "SELECT COUNT(*) FROM members WHERE status = 'Active' AND (@divisionId IS NULL OR division_id = @divisionId)",
            divisionId,
            null,
            cancellationToken);
        var activityCount = await GetCountAsync(
            connection,
            "SELECT COUNT(*) FROM activities WHERE (@divisionId IS NULL OR division_id = @divisionId)",
            divisionId,
            null,
            cancellationToken);
        var upcomingActivityCount = await GetCountAsync(
            connection,
            """
            SELECT COUNT(*) FROM activities
            WHERE status IN ('Upcoming', 'Pending')
              AND activity_date >= CURDATE()
              AND (@divisionId IS NULL OR division_id = @divisionId)
            """,
            divisionId,
            null,
            cancellationToken);
        var recentMemberCount = await GetCountAsync(
            connection,
            """
            SELECT COUNT(*) FROM members
            WHERE date_joined >= @sinceDate
              AND date_joined <= CURDATE()
              AND (@divisionId IS NULL OR division_id = @divisionId)
            """,
            divisionId,
            today.AddDays(-29),
            cancellationToken);
        var attendanceRecordedCount = await GetCountAsync(
            connection,
            """
            SELECT COUNT(*) FROM activities
            WHERE attendance_done = 1
              AND (@divisionId IS NULL OR division_id = @divisionId)
            """,
            divisionId,
            null,
            cancellationToken);

        return new DashboardData
        {
            MemberCount = memberCount,
            ActiveMemberCount = activeMemberCount,
            ActivityCount = activityCount,
            UpcomingActivityCount = upcomingActivityCount,
            NewMemberCount30Days = recentMemberCount,
            AttendanceRecordedCount = attendanceRecordedCount,
            UpcomingActivities = await GetActivitiesAsync(
                connection,
                divisionId,
                upcoming: true,
                cancellationToken),
            RecentActivities = await GetActivitiesAsync(
                connection,
                divisionId,
                upcoming: false,
                cancellationToken),
            RecentMembers = await GetRecentMembersAsync(connection, divisionId, cancellationToken),
            MonthlyTrend = await GetMonthlyTrendAsync(
                connection,
                divisionId,
                trendStart,
                trendEnd,
                cancellationToken)
        };
    }

    private static async Task<int> GetCountAsync(
        MySqlConnection connection,
        string query,
        int? divisionId,
        DateTime? sinceDate,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(query, connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        if (sinceDate.HasValue)
        {
            command.Parameters.Add("@sinceDate", MySqlDbType.Date).Value = sinceDate.Value.Date;
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task<IReadOnlyList<DashboardActivity>> GetActivitiesAsync(
        MySqlConnection connection,
        int? divisionId,
        bool upcoming,
        CancellationToken cancellationToken)
    {
        var dateFilter = upcoming
            ? "AND activity_date >= CURDATE() AND status IN ('Upcoming', 'Pending')"
            : "AND activity_date < CURDATE()";
        var order = upcoming ? "ASC" : "DESC";
        await using var command = new MySqlCommand(
            $"""
            SELECT a.activity_name, a.activity_date, a.location, a.status, d.name AS division_name
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            WHERE (@divisionId IS NULL OR a.division_id = @divisionId)
              {dateFilter}
            ORDER BY a.activity_date {order}, a.activity_id DESC
            LIMIT 6
            """,
            connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var activities = new List<DashboardActivity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(new DashboardActivity
            {
                ActivityName = reader.GetString("activity_name"),
                ActivityDate = reader.GetDateTime("activity_date"),
                Location = reader.GetString("location"),
                Status = reader.GetString("status"),
                DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name")
            });
        }

        return activities;
    }

    private static async Task<IReadOnlyList<DashboardMember>> GetRecentMembersAsync(
        MySqlConnection connection,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.course, m.date_joined, d.name AS division_name
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            WHERE (@divisionId IS NULL OR m.division_id = @divisionId)
            ORDER BY m.date_joined DESC, m.member_id DESC
            LIMIT 6
            """,
            connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var members = new List<DashboardMember>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new DashboardMember
            {
                MemberId = reader.GetString("member_id"),
                StudentName = reader.GetString("student_name"),
                Course = reader.GetString("course"),
                DateJoined = reader.GetDateTime("date_joined"),
                DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name")
            });
        }

        return members;
    }

    private static async Task<IReadOnlyList<DashboardTrendPoint>> GetMonthlyTrendAsync(
        MySqlConnection connection,
        int? divisionId,
        DateTime trendStart,
        DateTime trendEnd,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<DateTime, (int Members, int Activities)>();
        await using (var command = new MySqlCommand(
            """
            SELECT CAST(DATE_FORMAT(date_joined, '%Y-%m-01') AS DATE) AS month_start, COUNT(*) AS total
            FROM members
            WHERE date_joined >= @trendStart
              AND date_joined < @trendEnd
              AND (@divisionId IS NULL OR division_id = @divisionId)
            GROUP BY month_start
            """,
            connection))
        {
            command.Parameters.Add("@trendStart", MySqlDbType.Date).Value = trendStart;
            command.Parameters.Add("@trendEnd", MySqlDbType.Date).Value = trendEnd;
            command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var month = reader.GetDateTime("month_start");
                counts[month] = (reader.GetInt32("total"), 0);
            }
        }

        await using (var command = new MySqlCommand(
            """
            SELECT CAST(DATE_FORMAT(activity_date, '%Y-%m-01') AS DATE) AS month_start, COUNT(*) AS total
            FROM activities
            WHERE activity_date >= @trendStart
              AND activity_date < @trendEnd
              AND (@divisionId IS NULL OR division_id = @divisionId)
            GROUP BY month_start
            """,
            connection))
        {
            command.Parameters.Add("@trendStart", MySqlDbType.Date).Value = trendStart;
            command.Parameters.Add("@trendEnd", MySqlDbType.Date).Value = trendEnd;
            command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var month = reader.GetDateTime("month_start");
                var memberCount = counts.TryGetValue(month, out var current) ? current.Members : 0;
                counts[month] = (memberCount, reader.GetInt32("total"));
            }
        }

        var points = new List<DashboardTrendPoint>(12);
        for (var month = trendStart; month < trendEnd; month = month.AddMonths(1))
        {
            counts.TryGetValue(month, out var values);
            points.Add(new DashboardTrendPoint
            {
                Month = month,
                NewMembers = values.Members,
                Activities = values.Activities
            });
        }

        return points;
    }
}
