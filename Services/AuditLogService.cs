using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface IAuditLogService
{
    Task<AuditLogListResponse> GetListAsync(
        int? skpdId,
        string? search,
        string? action,
        string? entityType,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

public sealed class AuditLogService(IMySqlConnectionFactory connectionFactory) : IAuditLogService
{
    public async Task<AuditLogListResponse> GetListAsync(
        int? skpdId,
        string? search,
        string? action,
        string? entityType,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var offset = (page - 1) * pageSize;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Build WHERE clauses
        var conditions = new List<string>();
        if (skpdId.HasValue)
        {
            conditions.Add("a.skpd_id = @skpdId");
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(a.identity LIKE @search OR a.action LIKE @search OR a.entity_type LIKE @search)");
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            conditions.Add("a.action = @action");
        }
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            conditions.Add("a.entity_type = @entityType");
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("a.status = @status");
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        // Count total
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM audit_logs a {where}";
        AddFilterParams(countCmd, skpdId, search, action, entityType, status);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Fetch page
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT
                a.id, a.user_id, a.skpd_id, s.nama AS skpd_nama,
                a.action, a.event_type, a.status, a.reason, a.identity,
                a.entity_type, a.entity_id,
                a.old_data, a.new_data,
                a.ip_address, a.user_agent, a.created_at
            FROM audit_logs a
            LEFT JOIN skpd s ON s.id = a.skpd_id
            {where}
            ORDER BY a.created_at DESC
            LIMIT @pageSize OFFSET @offset";

        AddFilterParams(cmd, skpdId, search, action, entityType, status);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        var items = new List<AuditLogItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new AuditLogItem
            {
                Id = reader.GetInt64("id"),
                UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ? null : reader.GetInt64("user_id"),
                SkpdId = reader.IsDBNull(reader.GetOrdinal("skpd_id")) ? null : reader.GetInt32("skpd_id"),
                SkpdNama = reader.IsDBNull(reader.GetOrdinal("skpd_nama")) ? null : reader.GetString("skpd_nama"),
                Action = reader.GetString("action"),
                EventType = reader.GetString("event_type"),
                Status = reader.GetString("status"),
                Reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? string.Empty : reader.GetString("reason"),
                Identity = reader.GetString("identity"),
                EntityType = reader.IsDBNull(reader.GetOrdinal("entity_type")) ? null : reader.GetString("entity_type"),
                EntityId = reader.IsDBNull(reader.GetOrdinal("entity_id")) ? null : reader.GetInt64("entity_id"),
                OldData = reader.IsDBNull(reader.GetOrdinal("old_data")) ? null : reader.GetString("old_data"),
                NewData = reader.IsDBNull(reader.GetOrdinal("new_data")) ? null : reader.GetString("new_data"),
                IpAddress = reader.GetString("ip_address"),
                UserAgent = reader.GetString("user_agent"),
                CreatedAt = reader.GetDateTime("created_at"),
            });
        }

        return new AuditLogListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
        };
    }

    private static void AddFilterParams(
        MySqlConnector.MySqlCommand cmd,
        int? skpdId, string? search, string? action, string? entityType, string? status)
    {
        if (skpdId.HasValue)
            cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
        if (!string.IsNullOrWhiteSpace(action))
            cmd.Parameters.AddWithValue("@action", action.Trim());
        if (!string.IsNullOrWhiteSpace(entityType))
            cmd.Parameters.AddWithValue("@entityType", entityType.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            cmd.Parameters.AddWithValue("@status", status.Trim());
    }
}
