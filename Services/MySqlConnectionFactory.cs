using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class MySqlConnectionFactory(IConfiguration configuration)
{
    public MySqlConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("SOMAMS");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The SOMAMS database connection is not configured. Set ConnectionStrings__SOMAMS.");
        }

        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            ConnectionTimeout = 5
        };

        return new MySqlConnection(builder.ConnectionString);
    }
}
