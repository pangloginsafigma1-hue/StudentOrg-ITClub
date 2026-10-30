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
            SELECT a.admin_id, a.full_name, a.username, a.email, a.role,
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

    public async Task<AdminAccountFormViewModel?> GetByIdAsync(
        int adminId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT admin_id, full_name, username, email, role, division_id, is_active
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
            Role = reader.GetString("role"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            IsActive = reader.GetBoolean("is_active"),
            Divisions = await divisionRepository.GetAllAsync(cancellationToken)
        };
        return account;
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

    public async Task CreateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            INSERT INTO admins (full_name, username, email, password_hash, role, division_id, is_active)
            VALUES (@fullName, @username, @email, @passwordHash, @role, @divisionId, @isActive)
            """,
            connection);
        AddAccountParameters(command, account);
        command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255).Value =
            BCrypt.Net.BCrypt.HashPassword(account.Password);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(AdminAccountFormViewModel account, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await WouldRemoveLastActiveSuperAdminAsync(connection, transaction, account, cancellationToken))
            {
                throw new InvalidOperationException("At least one active super admin account must remain.");
            }

            var updates = new List<string>
            {
                "full_name = @fullName",
                "username = @username",
                "email = @email",
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

    private static void AddAccountParameters(MySqlCommand command, AdminAccountFormViewModel account)
    {
        command.Parameters.Add("@fullName", MySqlDbType.VarChar, 100).Value = account.FullName.Trim();
        command.Parameters.Add("@username", MySqlDbType.VarChar, 50).Value = account.Username.Trim();
        command.Parameters.Add("@email", MySqlDbType.VarChar, 100).Value = account.Email.Trim();
        command.Parameters.Add("@role", MySqlDbType.VarChar, 30).Value = account.Role;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value =
            (object?)account.DivisionId ?? DBNull.Value;
        command.Parameters.Add("@isActive", MySqlDbType.Bool).Value = account.IsActive;
    }
}
