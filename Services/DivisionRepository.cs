using ITClubOrg.Models;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class DivisionRepository(MySqlConnectionFactory connectionFactory) : IDivisionRepository
{
    public async Task<IReadOnlyList<Division>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT division_id, name FROM divisions WHERE is_active = 1 ORDER BY name",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var divisions = new List<Division>();
        while (await reader.ReadAsync(cancellationToken))
        {
            divisions.Add(new Division
            {
                DivisionId = reader.GetInt32("division_id"),
                Name = reader.GetString("name")
            });
        }

        return divisions;
    }
}
