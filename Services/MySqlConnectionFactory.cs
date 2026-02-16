using MySqlConnector;

namespace skpd_multi_tenant_api.Services;

public interface IMySqlConnectionFactory
{
    Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class MySqlConnectionFactory(IConfiguration configuration) : IMySqlConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("MySql")
        ?? throw new InvalidOperationException("ConnectionStrings:MySql belum diset.");

    public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
