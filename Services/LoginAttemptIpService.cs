using skpd_multi_tenant_api.Models;
using MySqlConnector;
using System.Data;

namespace skpd_multi_tenant_api.Services
{
    public interface ILoginAttemptIpService
    {
        Task<LoginAttemptIpResponse> GetAllAsync(int page, int pageSize, string? search, CancellationToken ct = default);
        Task<bool> DeleteAsync(long id, CancellationToken ct = default);
        Task<bool> ClearAllAsync(CancellationToken ct = default);
    }

    public class LoginAttemptIpService(IMySqlConnectionFactory connectionFactory, ILogger<LoginAttemptIpService> logger) : ILoginAttemptIpService
    {
        public async Task<LoginAttemptIpResponse> GetAllAsync(int page, int pageSize, string? search, CancellationToken ct = default)
        {
            logger.LogInformation("LoginAttemptIpService.GetAllAsync: page={page}, pageSize={pageSize}, search={search}", page, pageSize, search);
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
            
            var whereClause = string.IsNullOrEmpty(search) 
                ? "" 
                : "WHERE ip_address LIKE @search OR email LIKE @search OR failure_reason LIKE @search";

            var countSql = $@"SELECT COUNT(*) FROM login_attempts_ip {whereClause}";
            var dataSql = $@"
                SELECT id, email, ip_address, user_agent, success, failure_reason, attempt_time 
                FROM login_attempts_ip 
                {whereClause}
                ORDER BY attempt_time DESC
                LIMIT @offset, @limit";

            await using var countCmd = connection.CreateCommand();
            countCmd.CommandText = countSql;
            if (!string.IsNullOrEmpty(search)) countCmd.Parameters.AddWithValue("@search", $"%{search}%");
            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

            await using var dataCmd = connection.CreateCommand();
            dataCmd.CommandText = dataSql;
            if (!string.IsNullOrEmpty(search)) dataCmd.Parameters.AddWithValue("@search", $"%{search}%");
            dataCmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
            dataCmd.Parameters.AddWithValue("@limit", pageSize);

            var items = new List<LoginAttemptIp>();
            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new LoginAttemptIp
                {
                    Id = reader.GetInt64("id"),
                    Email = reader.IsDBNull("email") ? null : reader.GetString("email"),
                    IpAddress = reader.GetString("ip_address"),
                    UserAgent = reader.GetString("user_agent"),
                    Success = reader.GetBoolean("success"),
                    FailureReason = reader.IsDBNull("failure_reason") ? null : reader.GetString("failure_reason"),
                    AttemptTime = reader.GetDateTime("attempt_time")
                });
            }

            logger.LogInformation("LoginAttemptIpService.GetAllAsync: Returning {count} items. TotalCount={total}", items.Count, totalCount);

            return new LoginAttemptIpResponse
            {
                Items = items,
                Total = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM login_attempts_ip WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        public async Task<bool> ClearAllAsync(CancellationToken ct = default)
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "TRUNCATE TABLE login_attempts_ip";
            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
    }
}
