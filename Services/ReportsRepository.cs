using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class ReportsRepository(MySqlConnectionFactory connectionFactory) : IReportsRepository
{
    private static string DivisionFilter(string column, IReadOnlyList<int> divisionIds, string? secondaryColumn = null) =>
        divisionIds.Count == 0
            ? "1 = 1"
            : $"({column} IN ({string.Join(", ", divisionIds.Select((_, index) => $"@division{index}"))})" +
              (secondaryColumn is null
                  ? ")"
                  : $" OR {secondaryColumn} IN ({string.Join(", ", divisionIds.Select((_, index) => $"@division{index}"))}))");

    private static void AddDivisionParameters(MySqlCommand command, IReadOnlyList<int> divisionIds)
    {
        for (var index = 0; index < divisionIds.Count; index++)
        {
            command.Parameters.Add($"@division{index}", MySqlDbType.Int32).Value = divisionIds[index];
        }
    }

    public async Task<IReadOnlyList<ReportMemberOption>> GetMembersAsync(
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            $"SELECT member_id, student_name FROM members WHERE {DivisionFilter("division_id", divisionIds)} ORDER BY student_name",
            connection);
        AddDivisionParameters(command, divisionIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var members = new List<ReportMemberOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new ReportMemberOption
            {
                MemberId = reader.GetString("member_id"),
                StudentName = reader.GetString("student_name")
            });
        }

        return members;
    }

    public async Task<IReadOnlyList<ReportActivityOption>> GetRecordedActivitiesAsync(
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT activity_id, activity_name, activity_start_at, activity_end_at
            FROM activities
            WHERE attendance_done = 1
              AND {DivisionFilter("division_id", divisionIds, "division_id_2")}
            ORDER BY activity_start_at DESC, activity_name
            """,
            connection);
        AddDivisionParameters(command, divisionIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var activities = new List<ReportActivityOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(new ReportActivityOption
            {
                ActivityId = reader.GetInt32("activity_id"),
                ActivityName = reader.GetString("activity_name"),
                ActivityDate = reader.GetDateTime("activity_start_at"),
                ActivityEndDate = reader.GetDateTime("activity_end_at")
            });
        }

        return activities;
    }

    public async Task<ReportTable> GetMembersReportAsync(
        string? memberId,
        string? position,
        string? status,
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT member_id, student_name, course, year_level, position, status, date_joined
            FROM members
            WHERE (@memberId IS NULL OR member_id = @memberId)
              AND (@position IS NULL OR position = @position)
              AND (@status IS NULL OR status = @status)
              AND {DivisionFilter("division_id", divisionIds)}
            ORDER BY student_name
            """,
            connection);
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = (object?)memberId ?? DBNull.Value;
        command.Parameters.Add("@position", MySqlDbType.VarChar, 50).Value = (object?)position ?? DBNull.Value;
        command.Parameters.Add("@status", MySqlDbType.VarChar, 8).Value = (object?)status ?? DBNull.Value;
        AddDivisionParameters(command, divisionIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<IReadOnlyList<string>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add([
                reader.GetString("member_id"),
                reader.GetString("student_name"),
                reader.GetString("course"),
                reader.GetString("year_level"),
                reader.GetString("position"),
                reader.GetString("status"),
                reader.GetDateTime("date_joined").ToString("MMM d, yyyy")
            ]);
        }

        return new ReportTable
        {
            Headers = ["ID", "Student Name", "Course", "Year Level", "Position", "Status", "Date Joined"],
            Rows = rows
        };
    }

    public async Task<ReportTable> GetActivitiesReportAsync(
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT activity_name, activity_start_at, activity_end_at, location, status,
                   CASE WHEN attendance_done = 1 THEN 'Recorded' ELSE 'Not yet' END AS attendance,
                   COALESCE(description, '') AS description
            FROM activities
            WHERE activity_start_at >= @fromDate
              AND activity_start_at < @toDateExclusive
              AND {DivisionFilter("division_id", divisionIds)}
            ORDER BY activity_start_at, activity_name
            """,
            connection);
        command.Parameters.Add("@fromDate", MySqlDbType.DateTime).Value = fromDate.Date;
        command.Parameters.Add("@toDateExclusive", MySqlDbType.DateTime).Value = toDate.Date.AddDays(1);
        AddDivisionParameters(command, divisionIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<IReadOnlyList<string>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add([
                reader.GetString("activity_name"),
                $"{reader.GetDateTime("activity_start_at"):MMM d, yyyy h:mm tt} - {reader.GetDateTime("activity_end_at"):MMM d, yyyy h:mm tt}",
                reader.GetString("location"),
                reader.GetString("status"),
                reader.GetString("attendance"),
                reader.GetString("description")
            ]);
        }

        return new ReportTable
        {
            Headers = ["Activity", "Start - End", "Location", "Status", "Attendance", "Description"],
            Rows = rows
        };
    }

    public async Task<ReportTable?> GetAttendanceReportAsync(
        int activityId,
        IReadOnlyList<int> divisionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.course,
                   CASE WHEN a.is_present = 1 THEN 'Present' ELSE 'Absent' END AS attendance
            FROM activities activity
            JOIN members m ON m.status = 'Active'
                 AND (activity.division_id IS NULL
                      OR m.division_id = activity.division_id
                      OR m.division_id = activity.division_id_2)
            LEFT JOIN attendance a ON a.member_id = m.member_id AND a.activity_id = activity.activity_id
            WHERE activity.activity_id = @activityId
              AND activity.attendance_done = 1
              AND {DivisionFilter("activity.division_id", divisionIds, "activity.division_id_2")}
            ORDER BY m.student_name
            """,
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        AddDivisionParameters(command, divisionIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var rows = new List<IReadOnlyList<string>>();
        do
        {
            rows.Add([
                reader.GetString("member_id"),
                reader.GetString("student_name"),
                reader.GetString("course"),
                reader.GetString("attendance")
            ]);
        }
        while (await reader.ReadAsync(cancellationToken));

        return new ReportTable
        {
            Headers = ["ID", "Student Name", "Course", "Attendance"],
            Rows = rows
        };
    }
}
