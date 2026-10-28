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
            SELECT a.activity_id, a.activity_name, a.location, a.activity_date, a.status, a.description,
                   a.attendance_done, a.division_id, d.name AS division_name
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            WHERE (@divisionId IS NULL OR a.division_id = @divisionId)
              AND (@search = ''
                   OR a.activity_name LIKE @pattern
                   OR a.location LIKE @pattern
                   OR a.status LIKE @pattern)
            ORDER BY a.activity_date DESC, a.activity_id DESC
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
            SELECT a.activity_id, a.activity_name, a.location, a.activity_date, a.status, a.description,
                   a.attendance_done, a.division_id, d.name AS division_name
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            WHERE a.activity_id = @activityId
              AND (@divisionId IS NULL OR a.division_id = @divisionId)
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
        if (divisionId is int assignedDivisionId && activity.DivisionId != assignedDivisionId)
        {
            throw new InvalidOperationException("A division administrator can only assign activities to their own division.");
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = activity.ActivityId == 0
            ? """
              INSERT INTO activities (activity_name, location, activity_date, status, description, division_id)
              VALUES (@activityName, @location, @activityDate, @status, @description, @activityDivisionId)
              """
            : """
              UPDATE activities
              SET activity_name = @activityName,
                  location = @location,
                  activity_date = @activityDate,
                  status = @status,
                  description = @description,
                  division_id = @activityDivisionId
              WHERE activity_id = @activityId
                AND (@divisionId IS NULL OR division_id = @divisionId)
              """;
        AddActivityParameters(command, activity);
        command.Parameters.Add("@activityDivisionId", MySqlDbType.Int32).Value =
            (object?)activity.DivisionId ?? DBNull.Value;
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
            "DELETE FROM activities WHERE activity_id = @activityId AND (@divisionId IS NULL OR division_id = @divisionId)",
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceActivityOption>> GetPendingAttendanceAsync(
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT activity_id, activity_name, activity_date
            FROM activities
            WHERE attendance_done = 0
              AND (@divisionId IS NULL OR division_id = @divisionId)
            ORDER BY activity_date DESC, activity_id DESC
            """,
            connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var activities = new List<AttendanceActivityOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(MapAttendanceOption(reader));
        }

        return activities;
    }

    public async Task<AttendanceActivityOption?> GetPendingAttendanceByIdAsync(
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
            SELECT activity_id, activity_name, activity_date
            FROM activities
            WHERE activity_id = @activityId
              AND attendance_done = 0
              AND (@divisionId IS NULL OR division_id = @divisionId)
            LIMIT 1
            """,
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapAttendanceOption(reader) : null;
    }

    internal static Activity MapActivity(MySqlDataReader reader) =>
        new()
        {
            ActivityId = reader.GetInt32("activity_id"),
            ActivityName = reader.GetString("activity_name"),
            Location = reader.GetString("location"),
            ActivityDate = reader.GetDateTime("activity_date"),
            Status = reader.GetString("status"),
            Description = reader.IsDBNull(reader.GetOrdinal("description"))
                ? string.Empty
                : reader.GetString("description"),
            AttendanceDone = reader.GetBoolean("attendance_done"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                ? null
                : reader.GetString("division_name")
        };

    internal static AttendanceActivityOption MapAttendanceOption(MySqlDataReader reader) =>
        new()
        {
            ActivityId = reader.GetInt32("activity_id"),
            ActivityName = reader.GetString("activity_name"),
            ActivityDate = reader.GetDateTime("activity_date")
        };

    internal static void AddActivityParameters(MySqlCommand command, Activity activity)
    {
        command.Parameters.Add("@activityName", MySqlDbType.VarChar, 150).Value = activity.ActivityName;
        command.Parameters.Add("@location", MySqlDbType.VarChar, 150).Value = activity.Location;
        command.Parameters.Add("@activityDate", MySqlDbType.Date).Value = activity.ActivityDate.Date;
        command.Parameters.Add("@status", MySqlDbType.VarChar, 20).Value = activity.Status;
        command.Parameters.Add("@description", MySqlDbType.Text).Value =
            string.IsNullOrEmpty(activity.Description) ? DBNull.Value : activity.Description;
    }
}
