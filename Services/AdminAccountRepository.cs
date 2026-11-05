using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class AdminAccountRepository(
    MySqlConnectionFactory connectionFactory,
    IDivisionRepository divisionRepository) : IAdminAccountRepository
{
    public async Task RecordPositionTermAsync(int adminId, string position, DateTime termStart, DateTime? termEnd, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            @"INSERT INTO admin_position_history (admin_id, position, term_start, term_end)
               VALUES (@adminId, @position, @termStart, @termEnd)", connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        command.Parameters.Add("@position", MySqlDbType.VarChar, 100).Value = position.Trim();
        command.Parameters.Add("@termStart", MySqlDbType.Date).Value = termStart.Date;
        command.Parameters.Add("@termEnd", MySqlDbType.Date).Value = (object?)termEnd?.Date ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<AdminAccountListItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT a.admin_id, a.full_name, a.username, a.email, a.student_id,
                   a.course, a.year_level, a.position, a.date_joined, a.role,
                   a.division_id, d.name AS division_name, a.is_active,
                   h.position AS current_position, h.term_start AS current_term_start,
                   h.term_end AS current_term_end
            FROM admins a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            LEFT JOIN admin_position_history h
                ON h.history_id = (
                    SELECT h2.history_id
                    FROM admin_position_history h2
                    WHERE h2.admin_id = a.admin_id
                    ORDER BY h2.term_start DESC, h2.history_id DESC
                    LIMIT 1
                )
            ORDER BY a.role, d.name, a.full_name
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var accounts = new List<AdminAccountListItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new AdminAccountListItem
            {
                AdminId = reader.GetInt32("admin_id"),
                FullName = reader.GetString("full_name"),
                Username = reader.GetString("username"),
                Email = reader.GetString("email"),
                StudentId = reader.IsDBNull(reader.GetOrdinal("student_id"))
                    ? null
                    : reader.GetString("student_id"),
                Course = reader.IsDBNull(reader.GetOrdinal("course"))
                    ? null
                    : reader.GetString("course"),
                YearLevel = reader.IsDBNull(reader.GetOrdinal("year_level"))
                    ? null
                    : reader.GetString("year_level"),
                Position = reader.IsDBNull(reader.GetOrdinal("position"))
                    ? null
                    : reader.GetString("position"),
                DateJoined = reader.IsDBNull(reader.GetOrdinal("date_joined"))
                    ? null
                    : reader.GetDateTime("date_joined"),
                Role = reader.GetString("role"),
                DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                    ? null
                    : reader.GetInt32("division_id"),
                DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name"),
                IsActive = reader.GetBoolean("is_active")
                ,
                CurrentPosition = reader.IsDBNull(reader.GetOrdinal("current_position"))
                    ? null
                    : reader.GetString("current_position"),
                CurrentTermStart = reader.IsDBNull(reader.GetOrdinal("current_term_start"))
                    ? null
                    : reader.GetDateTime("current_term_start"),
                CurrentTermEnd = reader.IsDBNull(reader.GetOrdinal("current_term_end"))
                    ? null
                    : reader.GetDateTime("current_term_end")
            });
        }

        return accounts;
    }

    public async Task<IReadOnlyList<AdminReserveNomination>> GetReserveNominationsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT r.slot_key, r.member_id, m.student_name
            FROM admin_reserve_candidates r
            JOIN members m ON m.member_id = r.member_id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var nominations = new List<AdminReserveNomination>();
        while (await reader.ReadAsync(cancellationToken))
        {
            nominations.Add(new AdminReserveNomination
            {
                SlotKey = reader.GetString("slot_key"),
                MemberId = reader.GetString("member_id"),
                MemberName = reader.GetString("student_name")
            });
        }

        return nominations;
    }

    public async Task<IReadOnlyList<AdminReserveCandidate>> GetReserveCandidatesAsync(
        string slotKey,
        CancellationToken cancellationToken)
    {
        if (!AdminReserveSlots.IsValid(slotKey))
        {
            throw new ArgumentException("Choose a valid reserve slot.", nameof(slotKey));
        }

        var divisionName = GetReserveDivisionName(slotKey);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, d.name AS division_name
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            LEFT JOIN admin_reserve_candidates r ON r.member_id = m.member_id
            WHERE m.status = 'Active'
              AND NOT EXISTS (
                  SELECT 1 FROM admins a WHERE a.student_id = m.member_id
              )
              AND (@divisionName IS NULL OR d.name = @divisionName)
              AND (r.member_id IS NULL OR r.slot_key = @slotKey)
            ORDER BY m.student_name, m.member_id
            """,
            connection);
        command.Parameters.Add("@slotKey", MySqlDbType.VarChar, 50).Value = slotKey;
        command.Parameters.Add("@divisionName", MySqlDbType.VarChar, 100).Value =
            (object?)divisionName ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var candidates = new List<AdminReserveCandidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new AdminReserveCandidate
            {
                MemberId = reader.GetString("member_id"),
                FullName = reader.GetString("student_name"),
                DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name")
            });
        }

        return candidates;
    }

    public async Task SaveReserveNominationAsync(
        string slotKey,
        string? memberId,
        int updatedBy,
        CancellationToken cancellationToken)
    {
        if (!AdminReserveSlots.IsValid(slotKey) || updatedBy <= 0)
        {
            throw new ArgumentException("Choose a valid reserve slot and administrator.");
        }

        var divisionName = GetReserveDivisionName(slotKey);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(memberId))
        {
            await using var deleteCommand = new MySqlCommand(
                "DELETE FROM admin_reserve_candidates WHERE slot_key = @slotKey",
                connection,
                transaction);
            deleteCommand.Parameters.Add("@slotKey", MySqlDbType.VarChar, 50).Value = slotKey;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await using (var candidateCommand = new MySqlCommand(
            """
            SELECT m.member_id
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            WHERE m.member_id = @memberId
              AND m.status = 'Active'
              AND NOT EXISTS (
                  SELECT 1 FROM admins a WHERE a.student_id = m.member_id
              )
              AND (@divisionName IS NULL OR d.name = @divisionName)
            FOR UPDATE
            """,
            connection,
            transaction))
        {
            candidateCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
            candidateCommand.Parameters.Add("@divisionName", MySqlDbType.VarChar, 100).Value =
                (object?)divisionName ?? DBNull.Value;
            if (await candidateCommand.ExecuteScalarAsync(cancellationToken) is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("Choose an active member in the selected division who does not already have an administrator account.");
            }
        }

        await using (var duplicateCommand = new MySqlCommand(
            "SELECT slot_key FROM admin_reserve_candidates WHERE member_id = @memberId AND slot_key <> @slotKey FOR UPDATE",
            connection,
            transaction))
        {
            duplicateCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
            duplicateCommand.Parameters.Add("@slotKey", MySqlDbType.VarChar, 50).Value = slotKey;
            if (await duplicateCommand.ExecuteScalarAsync(cancellationToken) is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("A member can be a reserve candidate for only one role.");
            }
        }

        await using (var deleteCurrentCommand = new MySqlCommand(
            "DELETE FROM admin_reserve_candidates WHERE slot_key = @slotKey",
            connection,
            transaction))
        {
            deleteCurrentCommand.Parameters.Add("@slotKey", MySqlDbType.VarChar, 50).Value = slotKey;
            await deleteCurrentCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var saveCommand = new MySqlCommand(
            """
            INSERT INTO admin_reserve_candidates (slot_key, member_id, updated_by)
            VALUES (@slotKey, @memberId, @updatedBy)
            """,
            connection,
            transaction))
        {
            saveCommand.Parameters.Add("@slotKey", MySqlDbType.VarChar, 50).Value = slotKey;
            saveCommand.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
            saveCommand.Parameters.Add("@updatedBy", MySqlDbType.Int32).Value = updatedBy;
            await saveCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminMemberCandidate>> GetActiveMemberCandidatesAsync(
        string search,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.position, m.course, m.year_level,
                   m.date_joined, m.division_id, d.name AS division_name,
                   COALESCE(m.email, p.email) AS email,
                   CASE WHEN m.division_id IS NOT NULL
                         AND NOT EXISTS (
                             SELECT 1
                             FROM admins existing_account
                             WHERE existing_account.student_id = m.member_id
                         )
                         AND NOT EXISTS (
                             SELECT 1
                             FROM admins existing_admin
                             WHERE existing_admin.role = 'DivisionAdmin'
                               AND existing_admin.division_id = m.division_id
                         )
                        THEN 1 ELSE 0 END AS can_be_division_admin,
                   CASE WHEN NOT EXISTS (
                             SELECT 1
                             FROM admins existing_account
                             WHERE existing_account.student_id = m.member_id
                        )
                        THEN 1 ELSE 0 END AS can_be_super_admin
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            LEFT JOIN member_portal_accounts p ON p.member_id = m.member_id
            WHERE m.status = 'Active'
              AND (@search = ''
                   OR m.member_id LIKE @pattern
                   OR m.student_name LIKE @pattern
                   OR COALESCE(m.email, p.email) LIKE @pattern
                   OR m.course LIKE @pattern
                   OR m.year_level LIKE @pattern
                   OR m.position LIKE @pattern
                   OR d.name LIKE @pattern)
            ORDER BY m.student_name, m.member_id
            """,
            connection);
        command.Parameters.Add("@search", MySqlDbType.VarChar, 100).Value = search;
        command.Parameters.Add("@pattern", MySqlDbType.VarChar, 202).Value = $"%{search}%";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var candidates = new List<AdminMemberCandidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(MapAdminMemberCandidate(reader));
        }

        return candidates;
    }

    public async Task<AdminMemberCandidate?> GetActiveMemberCandidateAsync(
        string memberId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT m.member_id, m.student_name, m.position, m.course, m.year_level,
                   m.date_joined, m.division_id, d.name AS division_name,
                   COALESCE(m.email, p.email) AS email,
                   CASE WHEN m.division_id IS NOT NULL
                         AND NOT EXISTS (
                             SELECT 1
                             FROM admins existing_account
                             WHERE existing_account.student_id = m.member_id
                         )
                         AND NOT EXISTS (
                             SELECT 1
                             FROM admins existing_admin
                             WHERE existing_admin.role = 'DivisionAdmin'
                               AND existing_admin.division_id = m.division_id
                         )
                        THEN 1 ELSE 0 END AS can_be_division_admin,
                   CASE WHEN NOT EXISTS (
                             SELECT 1
                             FROM admins existing_account
                             WHERE existing_account.student_id = m.member_id
                        )
                        THEN 1 ELSE 0 END AS can_be_super_admin
            FROM members m
            LEFT JOIN divisions d ON d.division_id = m.division_id
            LEFT JOIN member_portal_accounts p ON p.member_id = m.member_id
            WHERE m.member_id = @memberId
              AND m.status = 'Active'
            LIMIT 1
            """,
            connection);
        command.Parameters.Add("@memberId", MySqlDbType.VarChar, 20).Value = memberId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapAdminMemberCandidate(reader)
            : null;
    }

    private static AdminMemberCandidate MapAdminMemberCandidate(MySqlDataReader reader) =>
        new()
        {
            MemberId = reader.GetString("member_id"),
            FullName = reader.GetString("student_name"),
            Position = reader.GetString("position"),
            Course = reader.GetString("course"),
            YearLevel = reader.GetString("year_level"),
            DateJoined = reader.GetDateTime("date_joined"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                ? null
                : reader.GetString("division_name"),
            Email = reader.IsDBNull(reader.GetOrdinal("email"))
                ? null
                : reader.GetString("email"),
            CanBeDivisionAdmin = reader.GetBoolean("can_be_division_admin"),
            CanBeSuperAdmin = reader.GetBoolean("can_be_super_admin")
        };

    private static string? GetReserveDivisionName(string slotKey) =>
        slotKey switch
        {
            AdminReserveSlots.Software => "Software",
            AdminReserveSlots.Hardware => "Hardware",
            AdminReserveSlots.MultimediaAndArts => "Multimedia & Arts",
            AdminReserveSlots.SuperAdmin => null,
            _ => throw new ArgumentException("Choose a valid reserve slot.", nameof(slotKey))
        };

    public async Task<AdminAccountFormViewModel?> GetByIdAsync(
        int adminId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT admin_id, full_name, username, email, student_id, course, year_level,
                   position, date_joined, role, division_id, is_active
            FROM admins
            WHERE admin_id = @adminId
            LIMIT 1
            """,
            connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var account = new AdminAccountFormViewModel
        {
            AdminId = reader.GetInt32("admin_id"),
            FullName = reader.GetString("full_name"),
            Username = reader.GetString("username"),
            Email = reader.GetString("email"),
            StudentId = reader.IsDBNull(reader.GetOrdinal("student_id"))
                ? null
                : reader.GetString("student_id"),
            Course = reader.IsDBNull(reader.GetOrdinal("course"))
                ? null
                : reader.GetString("course"),
            YearLevel = reader.IsDBNull(reader.GetOrdinal("year_level"))
                ? null
                : reader.GetString("year_level"),
            Position = reader.IsDBNull(reader.GetOrdinal("position"))
                ? null
                : reader.GetString("position"),
            DateJoined = reader.IsDBNull(reader.GetOrdinal("date_joined"))
                ? null
                : reader.GetDateTime("date_joined"),
            Role = reader.GetString("role"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            IsActive = reader.GetBoolean("is_active"),
            Divisions = await divisionRepository.GetAllAsync(cancellationToken)
        };
        return account;
    }

    public async Task<int> GetSuperAdminCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM admins WHERE role = 'SuperAdmin'",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<bool> IsUsernameTakenAsync(
        string username,
        int exceptAdminId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM admins WHERE username = @username AND admin_id <> @adminId",
            connection);
        command.Parameters.Add("@username", MySqlDbType.VarChar, 50).Value = username;
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = exceptAdminId;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<int>> GetAssignedDivisionIdsAsync(
        int exceptAdminId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT DISTINCT division_id
            FROM admins
            WHERE role = 'DivisionAdmin'
              AND division_id IS NOT NULL
              AND admin_id <> @adminId
            """,
            connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = exceptAdminId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var divisionIds = new List<int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            divisionIds.Add(reader.GetInt32("division_id"));
        }

        return divisionIds;
    }

    public async Task CreateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken)
    {
        if (account.Role is not ("SuperAdmin" or "DivisionAdmin"))
        {
            throw new InvalidOperationException("New accounts must be created with an administrator role.");
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockAdminRoleChangesAsync(connection, transaction, cancellationToken);
            if (account.Role == "SuperAdmin"
                && await GetLockedSuperAdminIdsAsync(connection, transaction, cancellationToken) is { Count: >= 2 })
            {
                throw new InvalidOperationException("Only two Super Admin accounts are allowed.");
            }

            await using var command = new MySqlCommand(
                """
                INSERT INTO admins
                    (full_name, username, email, student_id, course, year_level, position, date_joined,
                     password_hash, role, division_id, is_active)
                VALUES
                    (@fullName, @username, @email, @studentId, @course, @yearLevel, @position, @dateJoined,
                     @passwordHash, @role, @divisionId, @isActive)
                """,
                connection,
                transaction);
            AddAccountParameters(command, account);
            command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value =
                BCrypt.Net.BCrypt.HashPassword(
                    account.Password
                    ?? throw new InvalidOperationException("A temporary password is required to create an administrator account."));
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockAdminRoleChangesAsync(connection, transaction, cancellationToken);
            var superAdminIds = await GetLockedSuperAdminIdsAsync(
                connection,
                transaction,
                cancellationToken);
            if (account.Role == "SuperAdmin"
                && !superAdminIds.Contains(account.AdminId)
                && superAdminIds.Count >= 2)
            {
                throw new InvalidOperationException("Only two Super Admin accounts are allowed. Another user cannot be promoted until a Super Admin role is available.");
            }

            if (await WouldRemoveLastActiveSuperAdminAsync(
                    connection,
                    transaction,
                    account,
                    cancellationToken))
            {
                throw new InvalidOperationException("At least one active super admin account must remain.");
            }

            if (account.Role == "Member")
            {
                await ConvertAdminToMemberAsync(connection, transaction, account, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var updates = new List<string>
            {
                "full_name = @fullName",
                "username = @username",
                "email = @email",
                "student_id = @studentId",
                "course = @course",
                "year_level = @yearLevel",
                "position = @position",
                "date_joined = @dateJoined",
                "role = @role",
                "division_id = @divisionId",
                "is_active = @isActive"
            };
            if (!string.IsNullOrEmpty(account.Password))
            {
                updates.Add("password_hash = @passwordHash");
            }

            await using var command = new MySqlCommand(
                $"UPDATE admins SET {string.Join(", ", updates)} WHERE admin_id = @adminId",
                connection,
                transaction);
            AddAccountParameters(command, account);
            command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = account.AdminId;
            if (!string.IsNullOrEmpty(account.Password))
            {
                command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value =
                    BCrypt.Net.BCrypt.HashPassword(account.Password);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<bool> WouldRemoveLastActiveSuperAdminAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AdminAccountFormViewModel account,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT admin_id
            FROM admins
            WHERE role = 'SuperAdmin' AND is_active = 1
            FOR UPDATE
            """,
            connection,
            transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var activeSuperAdminIds = new HashSet<int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            activeSuperAdminIds.Add(reader.GetInt32("admin_id"));
        }

        return activeSuperAdminIds.Contains(account.AdminId)
            && (account.Role != "SuperAdmin" || !account.IsActive)
            && activeSuperAdminIds.Count == 1;
    }

    private static async Task<HashSet<int>> GetLockedSuperAdminIdsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT admin_id FROM admins WHERE role = 'SuperAdmin' FOR UPDATE",
            connection,
            transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var adminIds = new HashSet<int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            adminIds.Add(reader.GetInt32("admin_id"));
        }

        return adminIds;
    }

    private static async Task LockAdminRoleChangesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT admin_id FROM admins ORDER BY admin_id LIMIT 1 FOR UPDATE",
            connection,
            transaction);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ConvertAdminToMemberAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AdminAccountFormViewModel account,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.StudentId))
        {
            throw new InvalidOperationException("A student ID is required to convert this account to a member.");
        }

        await using (var memberCommand = new MySqlCommand(
            """
            INSERT INTO members
                (member_id, student_name, email, course, year_level, position, status, date_joined, division_id)
            VALUES
                (@studentId, @fullName, @email, @course, @yearLevel, 'Member', 'Active', @dateJoined, @divisionId)
            ON DUPLICATE KEY UPDATE
                student_name = VALUES(student_name),
                email = VALUES(email),
                course = COALESCE(VALUES(course), course),
                year_level = COALESCE(VALUES(year_level), year_level),
                position = 'Member',
                date_joined = COALESCE(VALUES(date_joined), date_joined),
                division_id = COALESCE(VALUES(division_id), division_id)
            """,
            connection,
            transaction))
        {
            memberCommand.Parameters.Add("@studentId", MySqlDbType.VarChar, 20).Value = account.StudentId.Trim();
            memberCommand.Parameters.Add("@fullName", MySqlDbType.VarChar, 100).Value = account.FullName.Trim();
            memberCommand.Parameters.Add("@email", MySqlDbType.VarChar, 254).Value = account.Email.Trim();
            memberCommand.Parameters.Add("@course", MySqlDbType.VarChar, 50).Value =
                (object?)NormalizeOptional(account.Course) ?? "BSIT";
            memberCommand.Parameters.Add("@yearLevel", MySqlDbType.VarChar, 20).Value =
                (object?)NormalizeOptional(account.YearLevel) ?? "1st Year";
            memberCommand.Parameters.Add("@dateJoined", MySqlDbType.Date).Value =
                (object?)account.DateJoined?.Date ?? DateTime.Today;
            memberCommand.Parameters.Add("@divisionId", MySqlDbType.Int32).Value =
                (object?)account.DivisionId ?? DBNull.Value;
            await memberCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var statusHistoryCommand = new MySqlCommand(
            """
            INSERT INTO member_status_history (member_id, status, effective_from)
            SELECT @studentId, 'Active', COALESCE(@dateJoined, CURRENT_DATE())
            WHERE NOT EXISTS (
                SELECT 1 FROM member_status_history WHERE member_id = @studentId
            )
            """,
            connection,
            transaction))
        {
            statusHistoryCommand.Parameters.Add("@studentId", MySqlDbType.VarChar, 20).Value = account.StudentId.Trim();
            statusHistoryCommand.Parameters.Add("@dateJoined", MySqlDbType.Date).Value =
                (object?)account.DateJoined?.Date ?? DBNull.Value;
            await statusHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var portalEmailCommand = new MySqlCommand(
            "UPDATE member_portal_accounts SET email = @email WHERE member_id = @studentId",
            connection,
            transaction))
        {
            portalEmailCommand.Parameters.Add("@email", MySqlDbType.VarChar, 254).Value = account.Email.Trim();
            portalEmailCommand.Parameters.Add("@studentId", MySqlDbType.VarChar, 20).Value = account.StudentId.Trim();
            await portalEmailCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteCommand = new MySqlCommand(
            "DELETE FROM admins WHERE admin_id = @adminId",
            connection,
            transaction))
        {
            deleteCommand.Parameters.Add("@adminId", MySqlDbType.Int32).Value = account.AdminId;
            if (await deleteCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("The administrator account could not be found for conversion.");
            }
        }
    }

    private static void AddAccountParameters(MySqlCommand command, AdminAccountFormViewModel account)
    {
        command.Parameters.Add("@fullName", MySqlDbType.VarChar, 100).Value = account.FullName.Trim();
        command.Parameters.Add("@username", MySqlDbType.VarChar, 50).Value = account.Username.Trim();
        command.Parameters.Add("@email", MySqlDbType.VarChar, 254).Value = account.Email.Trim();
        command.Parameters.Add("@studentId", MySqlDbType.VarChar, 20).Value =
            (object?)NormalizeOptional(account.StudentId) ?? DBNull.Value;
        command.Parameters.Add("@course", MySqlDbType.VarChar, 50).Value =
            (object?)NormalizeOptional(account.Course) ?? DBNull.Value;
        command.Parameters.Add("@yearLevel", MySqlDbType.VarChar, 20).Value =
            (object?)NormalizeOptional(account.YearLevel) ?? DBNull.Value;
        command.Parameters.Add("@position", MySqlDbType.VarChar, 50).Value =
            (object?)NormalizeOptional(account.Position) ?? DBNull.Value;
        command.Parameters.Add("@dateJoined", MySqlDbType.Date).Value =
            (object?)account.DateJoined?.Date ?? DBNull.Value;
        command.Parameters.Add("@role", MySqlDbType.VarChar, 30).Value = account.Role;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value =
            (object?)account.DivisionId ?? DBNull.Value;
        command.Parameters.Add("@isActive", MySqlDbType.Bool).Value = account.IsActive;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
