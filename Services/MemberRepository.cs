using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class MemberRepository(MySqlConnectionFactory connectionFactory) : IMemberRepository
{
    public async Task<IReadOnlyList<Member>> GetAllAsync(
        string search,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT m.member_id, m.student_name, m.course, m.year_level, m.position, m.status,
                   m.date_joined, m.division_id, d.name AS division_name
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            WHERE (@divisionId IS NULL OR m.division_id = @divisionId)
              AND (@search = ''
                   OR m.member_id LIKE @pattern
                   OR m.student_name LIKE @pattern
                   OR m.course LIKE @pattern
                   OR m.year_level LIKE @pattern
                   OR m.position LIKE @pattern
                   OR m.status LIKE @pattern)
            ORDER BY m.student_name
            """;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        command.Parameters.Add("@search", MySqlDbType.VarChar).Value = search;
        command.Parameters.Add("@pattern", MySqlDbType.VarChar).Value = $"%{search}%";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var members = new List<Member>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(MapMember(reader));
        }

        return members;
    }

    public async Task<Member?> GetByIdAsync(
        string memberId,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.course, m.year_level, m.position, m.status,
                   m.date_joined, m.division_id, d.name AS division_name
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            WHERE m.member_id = @memberId
              AND (@divisionId IS NULL OR m.division_id = @divisionId)
            LIMIT 1
            """,
            connection);
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapMember(reader) : null;
    }

    public async Task AddAsync(Member member, int? divisionId, CancellationToken cancellationToken)
    {
        EnsureDivisionAccess(member, divisionId);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(
            """
            INSERT INTO members
                (member_id, student_name, course, year_level, position, status, date_joined, division_id)
            VALUES
                (@memberId, @studentName, @course, @yearLevel, @position, @status, @dateJoined, @divisionId)
            """,
            connection);
        AddMemberParameters(command, member);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value =
            (object?)member.DivisionId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Member member, int? divisionId, CancellationToken cancellationToken)
    {
        EnsureDivisionAccess(member, divisionId);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(
            """
            UPDATE members
            SET student_name = @studentName,
                course = @course,
                year_level = @yearLevel,
                position = @position,
                status = @status,
                date_joined = @dateJoined,
                division_id = @memberDivisionId
            WHERE member_id = @memberId
              AND (@divisionId IS NULL OR division_id = @divisionId)
            """,
            connection);
        AddMemberParameters(command, member);
        command.Parameters.Add("@memberDivisionId", MySqlDbType.Int32).Value =
            (object?)member.DivisionId ?? DBNull.Value;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string memberId, int? divisionId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(
            "DELETE FROM members WHERE member_id = @memberId AND (@divisionId IS NULL OR division_id = @divisionId)",
            connection);
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordStatusAsync(string memberId, string status, string? reason, int? divisionId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var close = new MySqlCommand(
            "UPDATE member_status_history SET effective_to = CURRENT_TIMESTAMP() WHERE member_id = @memberId AND effective_to IS NULL", connection, transaction);
        close.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        await close.ExecuteNonQueryAsync(cancellationToken);
        await using var update = new MySqlCommand(
            "UPDATE members SET status = @status WHERE member_id = @memberId AND (@divisionId IS NULL OR division_id = @divisionId)", connection, transaction);
        update.Parameters.Add("@status", MySqlDbType.VarChar, 20).Value = status;
        update.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        update.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        if (await update.ExecuteNonQueryAsync(cancellationToken) == 1)
        {
            await using var insert = new MySqlCommand(
                "INSERT INTO member_status_history (member_id, status, effective_from, reason) VALUES (@memberId, @status, CURRENT_TIMESTAMP(), @reason)", connection, transaction);
            insert.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
            insert.Parameters.Add("@status", MySqlDbType.VarChar, 20).Value = status;
            insert.Parameters.Add("@reason", MySqlDbType.VarChar, 500).Value = (object?)reason ?? DBNull.Value;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static void AddMemberParameters(MySqlCommand command, Member member)
    {
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = member.MemberId;
        command.Parameters.Add("@studentName", MySqlDbType.VarChar, 100).Value = member.StudentName;
        command.Parameters.Add("@course", MySqlDbType.VarChar, 50).Value = member.Course;
        command.Parameters.Add("@yearLevel", MySqlDbType.VarChar, 20).Value = member.YearLevel;
        command.Parameters.Add("@position", MySqlDbType.VarChar, 50).Value = member.Position;
        command.Parameters.Add("@status", MySqlDbType.VarChar, 8).Value = member.Status;
        command.Parameters.Add("@dateJoined", MySqlDbType.Date).Value = member.DateJoined.Date;
    }

    private static void EnsureDivisionAccess(Member member, int? divisionId)
    {
        if (divisionId is int assignedDivisionId && member.DivisionId != assignedDivisionId)
        {
            throw new InvalidOperationException("A division administrator can only assign members to their own division.");
        }
    }

    private static Member MapMember(MySqlDataReader reader) =>
        new()
        {
            MemberId = reader.GetString("member_id"),
            StudentName = reader.GetString("student_name"),
            Course = reader.GetString("course"),
            YearLevel = reader.GetString("year_level"),
            Position = reader.GetString("position"),
            Status = reader.GetString("status"),
            DateJoined = reader.GetDateTime("date_joined"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                ? null
                : reader.GetString("division_name")
        };
}
