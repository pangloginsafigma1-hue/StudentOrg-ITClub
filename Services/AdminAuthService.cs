using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class AdminAuthService(
    MySqlConnectionFactory connectionFactory,
    IConfiguration configuration) : IAdminAuthService
{
    public async Task<AdminAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT a.admin_id, a.full_name, a.username, a.password_hash,
                   a.role, a.division_id, d.name AS division_name
            FROM admins a
            LEFT JOIN divisions d ON d.division_id = a.division_id AND d.is_active = 1
            WHERE a.username = @username
              AND a.is_active = 1
            LIMIT 1
            """;
        command.Parameters.Add("@username", MySqlDbType.VarChar).Value = username;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var admin = new AdminAccount
        {
            AdminId = reader.GetInt32("admin_id"),
            FullName = reader.GetString("full_name"),
            Username = reader.GetString("username"),
            PasswordHash = reader.GetString("password_hash"),
            Role = reader.GetString("role"),
            DivisionId = reader.IsDBNull(reader.GetOrdinal("division_id"))
                ? null
                : reader.GetInt32("division_id"),
            DivisionName = reader.IsDBNull(reader.GetOrdinal("division_name"))
                ? null
                : reader.GetString("division_name")
        };

        if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            return null;
        }

        if (admin.Role == "SuperAdmin" && admin.DivisionId is null)
        {
            return admin;
        }

        if (admin.Role == "DivisionAdmin"
            && admin.DivisionId is > 0
            && !string.IsNullOrWhiteSpace(admin.DivisionName))
        {
            return admin;
        }

        throw new InvalidOperationException("The administrator account has an invalid role or division assignment.");
    }

    public async Task<bool> IsAuthorizationCurrentAsync(
        int adminId,
        string role,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        if (adminId <= 0
            || role is not ("SuperAdmin" or "DivisionAdmin")
            || (role == "SuperAdmin" && divisionId is not null)
            || (role == "DivisionAdmin" && divisionId is not > 0))
        {
            return false;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM admins a
            LEFT JOIN divisions d ON d.division_id = a.division_id AND d.is_active = 1
            WHERE a.admin_id = @adminId
              AND a.is_active = 1
              AND a.role = @role
              AND ((@role = 'SuperAdmin' AND a.division_id IS NULL)
                   OR (@role = 'DivisionAdmin'
                       AND a.division_id = @divisionId
                       AND d.division_id IS NOT NULL))
            """,
            connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        command.Parameters.Add("@role", MySqlDbType.VarChar, 30).Value = role;
        command.Parameters.Add("@divisionId", MySqlDbType.Int32).Value = (object?)divisionId ?? DBNull.Value;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<bool> PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var countCommand = new MySqlCommand("SELECT COUNT(*) FROM admins", connection);
        var adminCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        if (adminCount > 0)
        {
            return true;
        }

        var username = configuration["BootstrapAdmin:Username"];
        var fullName = configuration["BootstrapAdmin:FullName"];
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(username)
            && string.IsNullOrWhiteSpace(fullName)
            && string.IsNullOrWhiteSpace(email)
            && string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(fullName)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || username.Length > 50
            || fullName.Length > 100
            || email.Length > 100
            || password.Length < 12)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin requires a username, full name, email, and a password of at least 12 characters.");
        }

        await using var insertCommand = new MySqlCommand(
            """
            INSERT INTO admins (full_name, username, email, password_hash)
            VALUES (@fullName, @username, @email, @passwordHash)
            """,
            connection);
        insertCommand.Parameters.Add("@fullName", MySqlDbType.VarChar).Value = fullName.Trim();
        insertCommand.Parameters.Add("@username", MySqlDbType.VarChar).Value = username.Trim();
        insertCommand.Parameters.Add("@email", MySqlDbType.VarChar).Value = email.Trim();
        insertCommand.Parameters.Add("@passwordHash", MySqlDbType.VarChar).Value = BCrypt.Net.BCrypt.HashPassword(password);

        try
        {
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            await using var retryCountCommand = new MySqlCommand("SELECT COUNT(*) FROM admins", connection);
            var retryCount = Convert.ToInt32(await retryCountCommand.ExecuteScalarAsync(cancellationToken));
            if (retryCount > 0)
            {
                return true;
            }

            throw;
        }
    }
}
