using ITClubOrg.Models;
using MySqlConnector;
using System.Text;

namespace ITClubOrg.Services;

public sealed class MemberPortalRepository(MySqlConnectionFactory connectionFactory) : IMemberPortalRepository
{
    public async Task<bool> RegisterAsync(MemberRegistrationViewModel model, CancellationToken cancellationToken)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var findCommand = new MySqlCommand(
            """
            SELECT status
            FROM member_registration_requests
            WHERE member_id = @memberId
            FOR UPDATE
            """,
            connection,
            transaction);
        findCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = model.MemberId;
        var existingStatus = await findCommand.ExecuteScalarAsync(cancellationToken);

        await using var accountCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM member_portal_accounts WHERE member_id = @memberId",
            connection,
            transaction);
        accountCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = model.MemberId;
        if (Convert.ToInt32(await accountCommand.ExecuteScalarAsync(cancellationToken)) != 0
            || existingStatus is not null and not DBNull && Equals(existingStatus, "Pending"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var command = new MySqlCommand(
            existingStatus is null or DBNull
                ? """
                  INSERT INTO member_registration_requests
                      (member_id, student_name, email, course, year_level, division_id, password_hash, status)
                  VALUES
                      (@memberId, @studentName, @email, @course, @yearLevel, @divisionId, @passwordHash, 'Pending')
                  """
                : """
                  UPDATE member_registration_requests
                  SET student_name = @studentName,
                      email = @email,
                      course = @course,
                      year_level = @yearLevel,
                      division_id = @divisionId,
                      password_hash = @passwordHash,
                      status = 'Pending',
                      submitted_at = CURRENT_TIMESTAMP,
                      reviewed_at = NULL,
                      reviewed_by = NULL
                  WHERE member_id = @memberId
                  """,
            connection,
            transaction);
        AddRegistrationParameters(command, model, passwordHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<Member?> AuthenticateAsync(
        string memberId,
        string password,
        CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(password) > 72)
        {
            return null;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.course, m.year_level, m.position, m.status,
                   m.date_joined, m.division_id, d.name AS division_name, a.password_hash
            FROM member_portal_accounts a
            JOIN members m ON m.member_id = a.member_id
            LEFT JOIN divisions d ON d.division_id = m.division_id
            WHERE a.member_id = @memberId
              AND a.is_active = 1
              AND m.status = 'Active'
            LIMIT 1
            """,
            connection);
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var passwordHash = reader.GetString("password_hash");
        if (!BCrypt.Net.BCrypt.Verify(password, passwordHash))
        {
            return null;
        }

        return MapMember(reader);
    }

    public async Task<bool> IsAuthorizationCurrentAsync(string memberId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM member_portal_accounts a
            JOIN members m ON m.member_id = a.member_id
            WHERE a.member_id = @memberId
              AND a.is_active = 1
              AND m.status = 'Active'
            """,
            connection);
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<MemberPortalViewModel> GetPortalAsync(string memberId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        Member? member;
        await using (var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.course, m.year_level, m.position, m.status,
                   m.date_joined, m.division_id, d.name AS division_name
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            WHERE m.member_id = @memberId AND m.status = 'Active'
            LIMIT 1
            """,
            connection))
        {
            command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            member = await reader.ReadAsync(cancellationToken) ? MapMember(reader) : null;
        }

        if (member is null)
        {
            throw new InvalidOperationException("The signed-in member record is no longer active.");
        }

        var upcomingActivities = new List<Activity>();
        await using (var command = new MySqlCommand(
            """
            SELECT a.activity_id, a.activity_name, a.location, a.activity_start_at, a.activity_end_at, a.status, a.description,
                   a.attendance_done, a.division_id, d.name AS division_name
            FROM activities a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            WHERE a.activity_start_at >= CURRENT_TIMESTAMP()
              AND a.status <> 'Completed'
              AND (a.division_id IS NULL OR a.division_id = @divisionId)
            ORDER BY a.activity_start_at, a.activity_id
            LIMIT 20
            """,
            connection))
        {
            command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)member.DivisionId ?? DBNull.Value;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                upcomingActivities.Add(ActivityRepository.MapActivity(reader));
            }
        }

        var attendance = new List<MemberAttendanceRecord>();
        await using (var command = new MySqlCommand(
            """
            SELECT a.activity_name, a.activity_start_at, a.activity_end_at, at.is_present
            FROM attendance at
            JOIN activities a ON a.activity_id = at.activity_id
            WHERE at.member_id = @memberId
            ORDER BY a.activity_start_at DESC, a.activity_id DESC
            LIMIT 100
            """,
            connection))
        {
            command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                attendance.Add(new MemberAttendanceRecord
                {
                    ActivityName = reader.GetString("activity_name"),
                    ActivityDate = reader.GetDateTime("activity_start_at"),
                    ActivityEndDate = reader.GetDateTime("activity_end_at"),
                    IsPresent = reader.GetBoolean("is_present")
                });
            }
        }

        var announcements = await ReadAnnouncementsAsync(connection, member.DivisionId, false, cancellationToken);
        return new MemberPortalViewModel
        {
            Member = member,
            UpcomingActivities = upcomingActivities,
            Attendance = attendance,
            Announcements = announcements
        };
    }

    public async Task<IReadOnlyList<MemberPortalAccountListItem>> GetPortalAccountsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT a.member_id, m.student_name, m.status, a.is_active, d.name AS division_name
            FROM member_portal_accounts a
            JOIN members m ON m.member_id = a.member_id
            LEFT JOIN divisions d ON d.division_id = m.division_id
            ORDER BY d.name, m.student_name
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var accounts = new List<MemberPortalAccountListItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new MemberPortalAccountListItem
            {
                MemberId = reader.GetString("member_id"),
                FullName = reader.GetString("student_name"),
                DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name"),
                MembershipStatus = reader.GetString("status"),
                IsPortalAccountActive = reader.GetBoolean("is_active")
            });
        }

        return accounts;
    }

    public async Task<bool> SetPortalAccountActiveAsync(
        string memberId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            UPDATE member_portal_accounts
            SET is_active = @isActive
            WHERE member_id = @memberId
            """,
            connection);
        command.Parameters.Add("@isActive", MySqlDbType.Bool).Value = isActive;
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> ResetPortalPasswordAsync(
        string memberId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            UPDATE member_portal_accounts
            SET password_hash = @passwordHash
            WHERE member_id = @memberId
            """,
            connection);
        command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value = passwordHash;
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> ChangePortalPasswordAsync(
        string memberId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(currentPassword) > 72
            || Encoding.UTF8.GetByteCount(newPassword) > 72)
        {
            return false;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var findCommand = new MySqlCommand(
            """
            SELECT password_hash
            FROM member_portal_accounts
            WHERE member_id = @memberId AND is_active = 1
            FOR UPDATE
            """,
            connection,
            transaction);
        findCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        var storedHash = await findCommand.ExecuteScalarAsync(cancellationToken);
        if (storedHash is not string passwordHash
            || !BCrypt.Net.BCrypt.Verify(currentPassword, passwordHash))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var updateCommand = new MySqlCommand(
            """
            UPDATE member_portal_accounts
            SET password_hash = @passwordHash
            WHERE member_id = @memberId AND is_active = 1
            """,
            connection,
            transaction);
        updateCommand.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value =
            BCrypt.Net.BCrypt.HashPassword(newPassword);
        updateCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<MemberAnnouncement>> GetAnnouncementsAsync(
        int? divisionId,
        bool includeAllDivisions,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await ReadAnnouncementsAsync(connection, divisionId, includeAllDivisions, cancellationToken);
    }

    public async Task CreateAnnouncementAsync(
        string title,
        string body,
        int? divisionId,
        int publishedBy,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            INSERT INTO announcements (title, body, division_id, published_by)
            VALUES (@title, @body, @divisionId, @publishedBy)
            """,
            connection);
        command.Parameters.Add("@title", MySqlDbType.VarChar, 150).Value = title;
        command.Parameters.Add("@body", MySqlDbType.Text).Value = body;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        command.Parameters.Add("@publishedBy", MySqlDbType.Int32).Value = publishedBy;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAnnouncementAsync(
        int announcementId,
        int? divisionId,
        bool includeAllDivisions,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            DELETE FROM announcements
            WHERE announcement_id = @announcementId
              AND (@includeAllDivisions = 1 OR division_id = @divisionId)
            """,
            connection);
        command.Parameters.Add("@announcementId", MySqlDbType.Int32).Value = announcementId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        command.Parameters.Add("@includeAllDivisions", MySqlDbType.Bool).Value = includeAllDivisions;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<MemberRegistrationRequest>> GetPendingRegistrationsAsync(
        int? divisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT r.member_id, r.student_name, r.course, r.year_level,
                   d.name AS division_name, r.submitted_at, r.email
            FROM member_registration_requests r
            JOIN divisions d ON d.division_id = r.division_id
            WHERE r.status = 'Pending'
              AND (@divisionId IS NULL OR r.division_id = @divisionId)
            ORDER BY r.submitted_at
            """,
            connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var requests = new List<MemberRegistrationRequest>();
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(new MemberRegistrationRequest
            {
                MemberId = reader.GetString("member_id"),
                StudentName = reader.GetString("student_name"),
                Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
                Course = reader.GetString("course"),
                YearLevel = reader.GetString("year_level"),
                DivisionName = reader.GetString("division_name"),
                SubmittedAt = reader.GetDateTime("submitted_at")
            });
        }

        return requests;
    }

    public async Task<RegistrationApprovalResult> ApproveRegistrationAsync(
        string memberId,
        int? divisionId,
        int reviewedBy,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var requestCommand = new MySqlCommand(
            """
            SELECT member_id, student_name, email, course, year_level, division_id, password_hash
            FROM member_registration_requests
            WHERE member_id = @memberId AND status = 'Pending'
              AND (@divisionId IS NULL OR division_id = @divisionId)
            FOR UPDATE
            """,
            connection,
            transaction);
        requestCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        requestCommand.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        await using var reader = await requestCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new RegistrationApprovalResult(false, null, string.Empty);
        }

        var request = new RegistrationRow(
            reader.GetString("member_id"),
            reader.GetString("student_name"),
            reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
            reader.GetString("course"),
            reader.GetString("year_level"),
            reader.GetInt32("division_id"),
            reader.GetString("password_hash"));
        await reader.DisposeAsync();

        await using var existingCommand = new MySqlCommand(
            """
            SELECT student_name, course, year_level, status, division_id
            FROM members
            WHERE member_id = @memberId
            FOR UPDATE
            """,
            connection,
            transaction);
        existingCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        await using var existingReader = await existingCommand.ExecuteReaderAsync(cancellationToken);
        var existingMember = await existingReader.ReadAsync(cancellationToken);
        var existingRecordMatches = false;
        if (existingMember)
        {
            existingRecordMatches = existingReader.GetString("status") == "Active"
                && existingReader.GetString("student_name").Equals(request.StudentName, StringComparison.OrdinalIgnoreCase)
                && existingReader.GetString("course").Equals(request.Course, StringComparison.Ordinal)
                && existingReader.GetString("year_level").Equals(request.YearLevel, StringComparison.Ordinal)
                && !existingReader.IsDBNull(existingReader.GetOrdinal("division_id"))
                && existingReader.GetInt32("division_id") == request.DivisionId;
        }
        await existingReader.DisposeAsync();

        if (existingMember && !existingRecordMatches)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                "This Student ID already has a member record with different details or division. Reconcile the record before approving the portal account.");
        }

        if (!existingMember)
        {
            await using var insertMember = new MySqlCommand(
                """
                INSERT INTO members
                    (member_id, student_name, course, year_level, position, status, date_joined, division_id)
                VALUES
                    (@memberId, @studentName, @course, @yearLevel, 'Member', 'Active', CURRENT_DATE(), @divisionId)
                """,
                connection,
                transaction);
            insertMember.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = request.MemberId;
            insertMember.Parameters.Add("@studentName", MySqlDbType.VarChar, 100).Value = request.StudentName;
            insertMember.Parameters.Add("@course", MySqlDbType.VarChar, 50).Value = request.Course;
            insertMember.Parameters.Add("@yearLevel", MySqlDbType.VarChar, 20).Value = request.YearLevel;
            insertMember.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = request.DivisionId;
            await insertMember.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var accountInsert = new MySqlCommand(
            """
            INSERT INTO member_portal_accounts (member_id, password_hash)
            VALUES (@memberId, @passwordHash)
            """,
            connection,
            transaction);
        accountInsert.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = request.MemberId;
        accountInsert.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value = request.PasswordHash;
        await accountInsert.ExecuteNonQueryAsync(cancellationToken);

        await using var approveCommand = new MySqlCommand(
            """
            UPDATE member_registration_requests
            SET status = 'Approved', reviewed_at = CURRENT_TIMESTAMP, reviewed_by = @reviewedBy
            WHERE member_id = @memberId AND status = 'Pending'
            """,
            connection,
            transaction);
        approveCommand.Parameters.Add("@reviewedBy", MySqlDbType.Int32).Value = reviewedBy;
        approveCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        if (await approveCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new RegistrationApprovalResult(false, null, string.Empty);
        }

        await transaction.CommitAsync(cancellationToken);
        return new RegistrationApprovalResult(true, request.Email, request.StudentName);
    }

    public async Task<bool> RejectRegistrationAsync(
        string memberId,
        int? divisionId,
        int reviewedBy,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            UPDATE member_registration_requests
            SET status = 'Rejected', reviewed_at = CURRENT_TIMESTAMP, reviewed_by = @reviewedBy
            WHERE member_id = @memberId
              AND status = 'Pending'
              AND (@divisionId IS NULL OR division_id = @divisionId)
            """,
            connection);
        command.Parameters.Add("@reviewedBy", MySqlDbType.Int32).Value = reviewedBy;
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<IReadOnlyList<MemberAnnouncement>> ReadAnnouncementsAsync(
        MySqlConnection connection,
        int? divisionId,
        bool includeAllDivisions,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT a.announcement_id, a.title, a.body, a.created_at, a.division_id,
                   d.name AS division_name, publisher.full_name AS publisher_name,
                   CASE publisher.role
                       WHEN 'SuperAdmin' THEN 'Super Admin'
                       WHEN 'DivisionAdmin' THEN 'Division Head'
                       ELSE publisher.role
                   END AS publisher_position
            FROM announcements a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            JOIN admins publisher ON publisher.admin_id = a.published_by
            WHERE @includeAllDivisions = 1 OR a.division_id IS NULL OR a.division_id = @divisionId
            ORDER BY a.created_at DESC
            LIMIT 50
            """,
            connection);
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        command.Parameters.Add("@includeAllDivisions", MySqlDbType.Bool).Value = includeAllDivisions;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var announcements = new List<MemberAnnouncement>();
        while (await reader.ReadAsync(cancellationToken))
        {
            announcements.Add(new MemberAnnouncement
            {
                AnnouncementId = reader.GetInt32("announcement_id"),
                DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                    ? null
                    : reader.GetInt32("division_id"),
                Title = reader.GetString("title"),
                Body = reader.GetString("body"),
                CreatedAt = reader.GetDateTime("created_at"),
                PublisherName = reader.GetString("publisher_name"),
                PublisherPosition = reader.GetString("publisher_position"),
                DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name")
            });
        }

        return announcements;
    }

    private static void AddRegistrationParameters(
        MySqlCommand command,
        MemberRegistrationViewModel model,
        string passwordHash)
    {
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = model.MemberId;
        command.Parameters.Add("@studentName", MySqlDbType.VarChar, 100).Value = model.StudentName.Trim();
        command.Parameters.Add("@email", MySqlDbType.VarChar, 254).Value = model.Email.Trim();
        command.Parameters.Add("@course", MySqlDbType.VarChar, 50).Value = model.Course;
        command.Parameters.Add("@yearLevel", MySqlDbType.VarChar, 20).Value = model.YearLevel;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = model.DivisionId!.Value;
        command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value = passwordHash;
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

    private sealed record RegistrationRow(
        string MemberId,
        string StudentName,
        string? Email,
        string Course,
        string YearLevel,
        int DivisionId,
        string PasswordHash);
}
