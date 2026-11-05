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
            """
            SELECT a.full_name, a.username, a.email, a.student_id, a.course, a.year_level,
                   a.position, a.date_joined, a.role, d.name AS division_name,
                   a.photo IS NOT NULL AND OCTET_LENGTH(a.photo) > 0 AS has_photo
            FROM admins a
            LEFT JOIN divisions d ON d.division_id = a.division_id
            WHERE a.admin_id = @adminId
            """,
            connection);
        command.Parameters.Add("@adminId", MySqlDbType.Int32).Value = adminId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var isSuperAdmin = reader.GetString("role") == "SuperAdmin";
        return new SettingsViewModel
        {
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
            Role = isSuperAdmin ? "Super Admin" : "Division Admin",
            DivisionName = isSuperAdmin
                ? "All divisions"
                : reader.IsDBNull(reader.GetOrdinal("division_name"))
                    ? null
                    : reader.GetString("division_name"),
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
            "email = @email",
            "student_id = @studentId",
            "course = @course",
            "year_level = @yearLevel",
            "position = @position",
            "date_joined = @dateJoined"
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
        command.Parameters.Add("@email", MySqlDbType.VarChar, 254).Value = profile.Email.Trim();
        command.Parameters.Add("@studentId", MySqlDbType.VarChar, 20).Value =
            (object?)Normalize(profile.StudentId) ?? DBNull.Value;
        command.Parameters.Add("@course", MySqlDbType.VarChar, 50).Value =
            (object?)Normalize(profile.Course) ?? DBNull.Value;
        command.Parameters.Add("@yearLevel", MySqlDbType.VarChar, 20).Value =
            (object?)Normalize(profile.YearLevel) ?? DBNull.Value;
        command.Parameters.Add("@position", MySqlDbType.VarChar, 50).Value =
            (object?)Normalize(profile.Position) ?? DBNull.Value;
        command.Parameters.Add("@dateJoined", MySqlDbType.Date).Value =
            (object?)profile.DateJoined?.Date ?? DBNull.Value;
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
