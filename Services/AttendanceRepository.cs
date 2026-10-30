using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class AttendanceRepository(MySqlConnectionFactory connectionFactory) : IAttendanceRepository
{
    public async Task<IReadOnlyList<AttendanceMember>> GetRosterAsync(
        int activityId,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name
            FROM members m
            JOIN activities a ON a.activity_id = @activityId
            WHERE m.status = 'Active'
              AND (a.division_id IS NULL OR m.division_id = a.division_id OR m.division_id = a.division_id_2)
              AND (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)
            ORDER BY m.student_name
            """,
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var members = new List<AttendanceMember>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new AttendanceMember
            {
                MemberId = reader.GetString("member_id"),
                StudentName = reader.GetString("student_name")
            });
        }

        return members;
    }

    public async Task<IReadOnlyList<AttendanceMember>> GetRecordedRosterAsync(
        int activityId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, attendance.is_present
            FROM attendance
            JOIN members m ON m.member_id = attendance.member_id
            WHERE attendance.activity_id = @activityId
            ORDER BY m.student_name
            """,
            connection);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var members = new List<AttendanceMember>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new AttendanceMember
            {
                MemberId = reader.GetString("member_id"),
                StudentName = reader.GetString("student_name"),
                IsPresent = reader.GetBoolean("is_present")
            });
        }

        return members;
    }

    public async Task<bool> SaveAsync(
        int activityId,
        IReadOnlyCollection<string> presentMemberIds,
        int? divisionId,
        bool allowEditRecorded,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var attendanceDone = await LockActivityAsync(
                connection,
                transaction,
                activityId,
                divisionId,
                cancellationToken);
            if (attendanceDone is null || attendanceDone.Value && !allowEditRecorded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var rosterMemberIds = attendanceDone.Value
                ? await GetRecordedMemberIdsAsync(connection, transaction, activityId, cancellationToken)
                : await GetActiveMemberIdsAsync(connection, transaction, activityId, divisionId, cancellationToken);
            var rosterMemberSet = rosterMemberIds.ToHashSet(StringComparer.Ordinal);
            if (presentMemberIds.Any(memberId => !rosterMemberSet.Contains(memberId)))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await SaveRosterAsync(
                connection,
                transaction,
                activityId,
                rosterMemberIds,
                presentMemberIds.ToHashSet(StringComparer.Ordinal),
                cancellationToken);

            if (!attendanceDone.Value && !await MarkActivityCompletedAsync(
                    connection,
                    transaction,
                    activityId,
                    cancellationToken))
            {
                throw new InvalidOperationException("Attendance could not be locked because the activity was updated concurrently.");
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<bool?> LockActivityAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int activityId,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT attendance_done
            FROM activities
            WHERE activity_id = @activityId
              AND (@divisionId IS NULL OR division_id = @divisionId)
            FOR UPDATE
            """,
            connection,
            transaction);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            return null;
        }

        return Convert.ToBoolean(result);
    }

    private static async Task<IReadOnlyList<string>> GetRecordedMemberIdsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int activityId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT member_id
            FROM attendance
            WHERE activity_id = @activityId
            ORDER BY member_id
            FOR UPDATE
            """,
            connection,
            transaction);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var memberIds = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            memberIds.Add(reader.GetString("member_id"));
        }

        return memberIds;
    }

    private static async Task<IReadOnlyList<string>> GetActiveMemberIdsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int activityId,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id
            FROM members m
            JOIN activities a ON a.activity_id = @activityId
            WHERE m.status = 'Active'
              AND (a.division_id IS NULL OR m.division_id = a.division_id OR m.division_id = a.division_id_2)
              AND (@divisionId IS NULL OR a.division_id = @divisionId OR a.division_id_2 = @divisionId)
            ORDER BY m.member_id
            FOR UPDATE
            """,
            connection,
            transaction);
        command.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var memberIds = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            memberIds.Add(reader.GetString("member_id"));
        }

        return memberIds;
    }

    private static async Task SaveRosterAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int activityId,
        IReadOnlyList<string> memberIds,
        HashSet<string> presentMemberIds,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO attendance (activity_id, member_id, is_present)
            VALUES (@activityId, @memberId, @isPresent)
            ON DUPLICATE KEY UPDATE is_present = @isPresent
            """,
            connection,
            transaction);
        var activityParameter = command.Parameters.Add("@activityId", MySqlDbType.Int32);
        var memberParameter = command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20);
        var presentParameter = command.Parameters.Add("@isPresent", MySqlDbType.Bool);

        activityParameter.Value = activityId;
        foreach (var memberId in memberIds)
        {
            memberParameter.Value = memberId;
            presentParameter.Value = presentMemberIds.Contains(memberId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> MarkActivityCompletedAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int activityId,
        CancellationToken cancellationToken)
    {
        await using var lockCommand = new MySqlCommand(
            """
            UPDATE activities
            SET attendance_done = 1,
                status = 'Completed'
            WHERE activity_id = @activityId
              AND attendance_done = 0
            """,
            connection,
            transaction);
        lockCommand.Parameters.Add("@activityId", MySqlDbType.Int32).Value = activityId;

        return await lockCommand.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
