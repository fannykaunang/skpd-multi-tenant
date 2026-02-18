using System.Security.Claims;
using System.Text.Json;
using skpd_multi_tenant_api.Extensions;

namespace skpd_multi_tenant_api.Services;

public interface IAuditService
{
    /// <summary>
    /// Catat aktivitas CUD ke tabel audit_logs.
    /// Kegagalan pencatatan tidak akan mengganggu operasi utama.
    /// </summary>
    Task LogAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        string action,
        string eventType,
        string entityType,
        long? entityId,
        string status = "success",
        string reason = "ok",
        object? oldData = null,
        object? newData = null,
        CancellationToken ct = default);
}

public sealed class AuditService(IMySqlConnectionFactory connectionFactory) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task LogAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        string action,
        string eventType,
        string entityType,
        long? entityId,
        string status = "success",
        string reason = "ok",
        object? oldData = null,
        object? newData = null,
        CancellationToken ct = default)
    {
        try
        {
            var userId = user.GetUserId();
            var skpdId = user.GetSkpdId();
            var identity = user.GetUsername() ?? user.FindFirst("sub")?.Value ?? "unknown";
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            var oldJson = oldData is null ? (object)DBNull.Value : JsonSerializer.Serialize(oldData, JsonOptions);
            var newJson = newData is null ? (object)DBNull.Value : JsonSerializer.Serialize(newData, JsonOptions);

            await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO audit_logs
                    (user_id, skpd_id, action, event_type, status, reason, identity,
                     entity_type, entity_id, old_data, new_data, ip_address, user_agent, created_at)
                VALUES
                    (@userId, @skpdId, @action, @eventType, @status, @reason, @identity,
                     @entityType, @entityId, @oldData, @newData, @ip, @ua, UTC_TIMESTAMP())";

            cmd.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@skpdId", skpdId.HasValue ? (object)skpdId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@eventType", eventType);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@reason", reason);
            cmd.Parameters.AddWithValue("@identity", identity);
            cmd.Parameters.AddWithValue("@entityType", entityType);
            cmd.Parameters.AddWithValue("@entityId", entityId.HasValue ? (object)entityId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@oldData", oldJson);
            cmd.Parameters.AddWithValue("@newData", newJson);
            cmd.Parameters.AddWithValue("@ip", ip);
            cmd.Parameters.AddWithValue("@ua", userAgent);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Audit logging tidak boleh mengganggu operasi utama
        }
    }
}
