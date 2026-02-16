using MySqlConnector;

namespace skpd_multi_tenant_api.Services;

public interface ITenantResolver
{
    Task<int?> ResolveSkpdIdAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

public sealed class TenantResolver(IMySqlConnectionFactory connectionFactory) : ITenantResolver
{
    public async Task<int?> ResolveSkpdIdAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var host = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(host))
        {
            host = httpContext.Request.Host.Host;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var slug = host.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id FROM skpd 
                                WHERE deleted_at IS NULL 
                                  AND is_active = 1
                                  AND (slug = @slug OR domain = @host)
                                LIMIT 1";
        command.Parameters.AddWithValue("@slug", slug);
        command.Parameters.AddWithValue("@host", host);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null ? null : Convert.ToInt32(scalar);
    }
}
