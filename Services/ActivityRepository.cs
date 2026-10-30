using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class ActivityRepository(MySqlConnectionFactory connectionFactory) : IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> GetAllAsync(
        string search,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT a.activity_id, a.activity_name, a.location, a.activity_start_at, a.activity_end_at, a.status, a.description,
                   a.attendance_done, a.division_id, a.division_id_2, d.name AS division_name, d2.name AS division_name_2
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            LEFT JOIN divisions d2 ON d2.division_id = a.division_id_2
            WHERE (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)
              AND (@search = ''
                   OR a.activity_name LIKE @pattern
                   OR a.location LIKE @pattern
                   OR a.status LIKE @pattern)
            ORDER BY a.activity_start_at DESC, a.activity_id DESC
            """;
        command.Parameters.Add("@search", MySqlDbType.VarChar).Value = search;
        command.Parameters.Add("@pattern", MySqlDbType.VarChar).Value = $"%{search}%";
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var activities = new List<Activity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(MapActivity(reader));
        }

        return activities;
    }

    public async Task<Activity?> GetByIdAsync(
        int activityId,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return null;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT a.activity_id, a.activity_name, a.location, a.activity_start_at, a.activity_end_at, a.status, a.description,
                   a.attendance_done, a.division_id, a.division_id_2, d.name AS division_name, d2.name AS division_name_2
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            LEFT JOIN divisions d2 ON d2.division_id = a.division_id_2
            WHERE a.activity_id = @activityId
              AND (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)
            LIMIT 1
            """,
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapActivity(reader) : null;
    }

    public async Task SaveAsync(Activity activity, int? divisionId, CancellationToken cancellationToken)
    {
        if (divisionId is int assignedDivisionId
            && activity.DivisionId != assignedDivisionId
            && activity.DivisionId2 != assignedDivisionId)
        {
            throw new InvalidOperationException("A division administrator can only assign activities to their own division.");
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = activity.ActivityId == 0
            ? """
              INSERT INTO activities
                  (activity_name, location, activity_date, activity_start_at, activity_end_at, status, description, division_id, division_id_2)
              VALUES
                  (@activityName, @location, @activityDate, @activityStartAt, @activityEndDate, @status, @description, @activityDivisionId, @activityDivisionId2)
              """
            : """
              UPDATE activities
              SET activity_name = @activityName,
                  location = @location,
                  activity_date = @activityDate,
              activity_start_at = @activityStartAt,
              activity_end_at = @activityEndDate,
              status = CASE WHEN attendance_done = 1 THEN 'Completed' ELSE @status END,
                  description = @description,
                  division_id = @activityDivisionId,
                  division_id_2 = @activityDivisionId2
              WHERE activity_id = @activityId
                AND (@divisionId IS NULL OR division_id = @divisionId OR division_id_2 = @divisionId)
              """;
        AddActivityParameters(command, activity);
        command.Parameters.Add("@activityDivisionId", MySqlDbType.Int32).Value =
            (object?)activity.DivisionId ?? DBNull.Value;
        command.Parameters.Add("@activityDivisionId2", MySqlDbType.Int32).Value =
            (object?)activity.DivisionId2 ?? DBNull.Value;
        if (activity.ActivityId > 0)
        {
            command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activity.ActivityId;
            command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(int activityId, int? divisionId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "DELETE FROM activities WHERE activity_id = @activityId AND (@divisionId IS NULL OR division_id = @divisionId OR division_id_2 = @divisionId)",
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceActivityOption>> GetAttendanceActivitiesAsync(
        int? divisionId,
        bool includeRecorded,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT a.activity_id, a.activity_name, a.activity_start_at, a.activity_end_at,
                   a.attendance_done, d.name AS division_name
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            WHERE (@includeRecorded = 1 OR a.attendance_done = 0)
              AND (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)
            ORDER BY a.activity_start_at DESC, a.activity_id DESC
            """,
            connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        command.Parameters.Add("@includeRecorded", MySqlDbType.Bool).Value = includeRecorded;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var activities = new List<AttendanceActivityOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(MapAttendanceOption(reader));
        }

        return activities;
    }

    internal static Activity MapActivity(MySqlDataReader reader) =>
        new()
        {
            ActivityId = reader.GetInt32("activity_id"),
            ActivityName = reader.GetString("activity_name"),
            Location = reader.GetString("location"),
            ActivityDate = reader.GetDateTime("activity_start_at"),
            ActivityEndDate = reader.GetDateTime("activity_end_at"),
            Status = reader.GetString("status"),
            Description = reader.IsDBNull(reader.GetOrdinal("description"))
                ? string.Empty
                : reader.GetString("description"),
            AttendanceDone = reader.GetBoolean("attendance_done"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            DivisionId2 = reader.IsDBNull(reader.GetOrdinal("division_id_2"))
                ? null
                : reader.GetInt32("division_id_2"),
            DivisionName = ReadDivisionNames(reader)
        };

    private static string? ReadDivisionNames(MySqlDataReader reader)
    {
        var names = new List<string>();
        if (!reader.IsDBNull(reader.GetOrdinal("division_name")))
            names.Add(reader.GetString("division_name"));
        if (!reader.IsDBNull(reader.GetOrdinal("division_name_2")))
            names.Add(reader.GetString("division_name_2"));
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    internal static AttendanceActivityOption MapAttendanceOption(MySqlDataReader reader) =>
        new()
        {
            ActivityId = reader.GetInt32("activity_id"),
            ActivityName = reader.GetString("activity_name"),
            ActivityDate = reader.GetDateTime("activity_start_at"),
            ActivityEndDate = reader.GetDateTime("activity_end_at"),
            AttendanceDone = reader.GetBoolean("attendance_done"),
            DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                ? null
                : reader.GetString("division_name")
        };

    internal static void AddActivityParameters(MySqlCommand command, Activity activity)
    {
        command.Parameters.Add("@activityName", MySqlDbType.VarChar, 150).Value = activity.ActivityName;
        command.Parameters.Add("@location", MySqlDbType.VarChar, 150).Value = activity.Location;
        command.Parameters.Add("@activityDate", MySqlDbType.Date).Value = activity.ActivityDate.Date;
        command.Parameters.Add("@activityStartAt", MySqlDbType.DateTime).Value = activity.ActivityDate;
        command.Parameters.Add("@activityEndDate", MySqlDbType.DateTime).Value = activity.ActivityEndDate;
        command.Parameters.Add("@status", MySqlDbType.VarChar, 20).Value = activity.Status;
        command.Parameters.Add("@description", MySqlDbType.Text).Value =
            activity.Description ?? string.Empty;
    }
}
