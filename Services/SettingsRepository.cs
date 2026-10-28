using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class SettingsRepository(MySqlConnectionFactory connectionFactory) : ISettingsRepository
{
    public async Task<SettingsViewModel?> GetProfileAsync(int adminId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT full_name, username, email, photo IS NOT NULL AND OCTET_LENGTH(photo) > 0 AS has_photo FROM admins WHERE admin_id = @adminId",
            connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SettingsViewModel
        {
            FullName = reader.GetString("full_name"),
            Username = reader.GetString("username"),
            Email = reader.GetString("email"),
            HasPhoto = reader.GetBoolean("has_photo")
        };
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

    public async Task<byte[]?> GetPhotoAsync(int adminId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT photo FROM admins WHERE admin_id = @adminId",
            connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is byte[] photo ? photo : null;
    }

    public async Task UpdateProfileAsync(
        int adminId,
        SettingsViewModel profile,
        byte[]? photo,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var updates = new List<string>
        {
            "full_name = @fullName",
            "username = @username",
            "email = @email"
        };
        if (photo is not null)
        {
            updates.Add("photo = @photo");
        }

        if (!string.IsNullOrEmpty(profile.NewPassword))
        {
            updates.Add("password_hash = @passwordHash");
        }

        command.CommandText = $"UPDATE admins SET {string.Join(", ", updates)} WHERE admin_id = @adminId";
        command.Parameters.Add("@fullName", MySqlDbType.VarChar, 100).Value = profile.FullName.Trim();
        command.Parameters.Add("@username", MySqlDbType.VarChar, 50).Value = profile.Username.Trim();
        command.Parameters.Add("@email", MySqlDbType.VarChar, 100).Value = profile.Email.Trim();
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        if (photo is not null)
        {
            command.Parameters.Add("@photo", MySqlDbType.LongBlob).Value = photo;
        }

        if (!string.IsNullOrEmpty(profile.NewPassword))
        {
            command.Parameters.Add("@passwordHash", MySqlDbType.VarChar, 255)
                .Value = BCrypt.Net.BCrypt.HashPassword(profile.NewPassword);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
