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
                   a.attendance_done, a.registration_open, a.division_id, a.division_id_2, d.name AS division_name, d2.name AS division_name_2
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
                   a.attendance_done, a.registration_open, a.division_id, a.division_id_2, d.name AS division_name, d2.name AS division_name_2
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
                  (activity_name, location, activity_date, activity_start_at, activity_end_at, status, description, registration_open, division_id, division_id_2)
              VALUES
                  (@activityName, @location, @activityDate, @activityStartAt, @activityEndDate, @status, @description, @registrationOpen, @activityDivisionId, @activityDivisionId2)
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
              registration_open = @registrationOpen,
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
        command.Parameters.Add("@registrationOpen", MySqlDbType.Bool).Value = activity.IsRegistrationOpen;
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

    public async Task<IReadOnlyList<ActivityRegistration>> GetRegistrationsAsync(int activityId, int? divisionId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            @"SELECT r.activity_id, a.activity_name, r.member_id, m.student_name, r.registered_at, r.participated,
                      r.participation_type, r.notes, r.recorded_by, r.recorded_at
               FROM activity_registrations r JOIN activities a ON a.activity_id = r.activity_id
               JOIN members m ON m.member_id = r.member_id
               WHERE r.activity_id = @activityId
                 AND (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)
               ORDER BY m.student_name", connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ActivityRegistration>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new ActivityRegistration
            {
                ActivityId = reader.GetInt32("activity_id"),
                ActivityName = reader.GetString("activity_name"),
                MemberId = reader.GetString("member_id"),
                MemberName = reader.GetString("student_name"),
                RegisteredAt = reader.GetDateTime("registered_at"),
                Participated = reader.GetBoolean("participated"),
                ParticipationType = reader.IsDBNull(reader.GetOrdinal("participation_type")) ? null : reader.GetString("participation_type"),
                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString("notes"),
                RecordedBy = reader.IsDBNull(reader.GetOrdinal("recorded_by")) ? null : reader.GetInt32("recorded_by"),
                RecordedAt = reader.IsDBNull(reader.GetOrdinal("recorded_at")) ? null : reader.GetDateTime("recorded_at")
            });
        return result;
    }

    public async Task<bool> SetParticipationAsync(
        int activityId,
        string memberId,
        bool participated,
        string participationType,
        string? notes,
        int recordedBy,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            @"UPDATE activity_registrations r JOIN activities a ON a.activity_id = r.activity_id
               SET r.participated = @participated,
                   r.participation_type = @participationType,
                   r.notes = @notes,
                   r.recorded_by = @recordedBy,
                   r.recorded_at = CURRENT_TIMESTAMP()
               WHERE r.activity_id = @activityId AND r.member_id = @memberId
                 AND (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)", connection);
        command.Parameters.Add("@participated", MySqlDbType.Bool).Value = participated;
        command.Parameters.Add("@participationType", MySqlDbType.VarChar, 30).Value = participationType;
        command.Parameters.Add("@notes", MySqlDbType.VarChar, 1000).Value = (object?)notes ?? DBNull.Value;
        command.Parameters.Add("@recordedBy", MySqlDbType.Int32).Value = recordedBy;
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
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
            IsRegistrationOpen = reader.GetBoolean("registration_open"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            DivisionId2 = reader.IsDBNull(reader.GetOrdinal("division_id_2"))
                ? null
                : reader.GetInt32("division_id_2"),
            DivisionName = ReadDivisionNames(reader),
            IsRegistered = HasColumn(reader, "is_registered") && reader.GetBoolean("is_registered")
        };

    private static bool HasColumn(MySqlDataReader reader, string name)
    {
        try { return reader.GetOrdinal(name) >= 0; }
        catch (IndexOutOfRangeException) { return false; }
    }

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
