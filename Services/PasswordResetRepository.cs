using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class PasswordResetRepository(MySqlConnectionFactory connectionFactory) : IPasswordResetRepository
{
    public async Task<PasswordResetAccount?> GetAccountByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT admin_id, email FROM admins WHERE email = @email LIMIT 1",
            connection);
        command.Parameters.Add("@email", MySqlDbType.VarChar, 100).Value = email;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PasswordResetAccount
        {
            AdminId = reader.GetInt32("admin_id"),
            Email = reader.GetString("email")
        };
    }

    public async Task<bool> ResetPasswordAsync(
        int adminId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "UPDATE admins SET password_hash = @passwordHash WHERE admin_id = @adminId",
            connection);
        command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255)
            .Value = BCrypt.Net.BCrypt.HashPassword(newPassword);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
