using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface INotificationService
{
    Task CreateAsync(string userId, string title, string message, string? link, string type, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(long userId, int limit, CancellationToken ct = default);
    Task<NotificationListResponse> GetPaginatedAsync(long userId, int page, int pageSize, string? search, string? type, string? isRead, CancellationToken ct = default);
    Task<NotificationStatsResponse> GetStatsAsync(long userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(long userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(long notificationId, long userId, CancellationToken ct = default);
    Task<int> DeleteBatchAsync(long[] ids, long userId, CancellationToken ct = default);
    Task DeleteAllAsync(long userId, CancellationToken ct = default);
    /// <summary>Users with role 'Admin' or 'Editor' in skpdId only (no SuperAdmin). Used when berita is created.</summary>
    Task<IReadOnlyList<long>> GetUsersToNotifyAsync(int skpdId, long excludeUserId, CancellationToken ct = default);
    /// <summary>Users with edit_berita permission scoped to skpdId only (editors, not SuperAdmin). Used when berita is updated.</summary>
    Task<IReadOnlyList<long>> GetEditorsBySkpdAsync(int skpdId, long excludeUserId, CancellationToken ct = default);
    /// <summary>Users with manage_all permission AND u.skpd_id = skpdId (SKPD-level admins, NOT SuperAdmin who has skpd_id = NULL). Used when berita is published.</summary>
    Task<IReadOnlyList<long>> GetSkpdAdminsAsync(int skpdId, long excludeUserId, CancellationToken ct = default);
}

public sealed class NotificationService(
    IMySqlConnectionFactory connectionFactory,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAsync(string userId, string title, string message, string? link, string type, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO notifications (user_id, title, message, link, type)
            VALUES (@userId, @title, @message, @link, @type)
            """;
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@message", message);
        command.Parameters.AddWithValue("@link", (object?)link ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", type);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(long userId, int limit, CancellationToken ct = default)
    {
        var items = new List<Notification>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, user_id, title, message, link, type, is_read, created_at
            FROM notifications
            WHERE user_id = @uid
            ORDER BY created_at DESC
            LIMIT @limit
            """;
        command.Parameters.AddWithValue("@uid", userId.ToString());
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new Notification
            {
                Id = reader.GetInt64("id"),
                UserId = reader.GetString("user_id"),
                Title = reader.GetString("title"),
                Message = reader.GetString("message"),
                Link = reader.IsDBNull(reader.GetOrdinal("link")) ? null : reader.GetString("link"),
                Type = reader.GetString("type"),
                IsRead = reader.GetBoolean("is_read"),
                CreatedAt = reader.GetDateTime("created_at")
            });
        }

        return items;
    }

    public async Task<NotificationListResponse> GetPaginatedAsync(
        long userId, int page, int pageSize, string? search, string? type, string? isRead, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        var conditions = new List<string> { "user_id = @uid" };
        if (!string.IsNullOrEmpty(search))
            conditions.Add("(title LIKE @search OR message LIKE @search)");
        if (!string.IsNullOrEmpty(type))
            conditions.Add("type = @type");
        if (isRead == "true")
            conditions.Add("is_read = 1");
        else if (isRead == "false")
            conditions.Add("is_read = 0");

        var where = "WHERE " + string.Join(" AND ", conditions);

        // Count
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM notifications {where}";
        countCmd.Parameters.AddWithValue("@uid", userId.ToString());
        if (!string.IsNullOrEmpty(search)) countCmd.Parameters.AddWithValue("@search", $"%{search}%");
        if (!string.IsNullOrEmpty(type)) countCmd.Parameters.AddWithValue("@type", type);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Data
        await using var dataCmd = connection.CreateCommand();
        dataCmd.CommandText = $@"
            SELECT id, user_id, title, message, link, type, is_read, created_at
            FROM notifications
            {where}
            ORDER BY created_at DESC
            LIMIT @offset, @limit";
        dataCmd.Parameters.AddWithValue("@uid", userId.ToString());
        if (!string.IsNullOrEmpty(search)) dataCmd.Parameters.AddWithValue("@search", $"%{search}%");
        if (!string.IsNullOrEmpty(type)) dataCmd.Parameters.AddWithValue("@type", type);
        dataCmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        dataCmd.Parameters.AddWithValue("@limit", pageSize);

        var items = new List<Notification>();
        await using var reader = await dataCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new Notification
            {
                Id = reader.GetInt64("id"),
                UserId = reader.GetString("user_id"),
                Title = reader.GetString("title"),
                Message = reader.GetString("message"),
                Link = reader.IsDBNull(reader.GetOrdinal("link")) ? null : reader.GetString("link"),
                Type = reader.GetString("type"),
                IsRead = reader.GetBoolean("is_read"),
                CreatedAt = reader.GetDateTime("created_at")
            });
        }

        return new NotificationListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationStatsResponse> GetStatsAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(*) as Total,
                SUM(CASE WHEN is_read = 0 THEN 1 ELSE 0 END) as Unread,
                SUM(CASE WHEN type = 'info' THEN 1 ELSE 0 END) as Info,
                SUM(CASE WHEN type = 'success' THEN 1 ELSE 0 END) as Success,
                SUM(CASE WHEN type = 'warning' THEN 1 ELSE 0 END) as Warning,
                SUM(CASE WHEN type = 'error' THEN 1 ELSE 0 END) as Error
            FROM notifications 
            WHERE user_id = @uid";
        command.Parameters.AddWithValue("@uid", userId.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct);
        var stats = new NotificationStatsResponse();
        if (await reader.ReadAsync(ct))
        {
            stats.Total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            stats.Unread = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            stats.Info = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            stats.Success = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            stats.Warning = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            stats.Error = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
        }
        return stats;
    }

    public async Task<bool> DeleteAsync(long notificationId, long userId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM notifications WHERE id = @id AND user_id = @uid";
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@uid", userId.ToString());
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<int> DeleteBatchAsync(long[] ids, long userId, CancellationToken ct = default)
    {
        if (ids.Length == 0) return 0;
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        var placeholders = string.Join(",", ids.Select((_, i) => $"@id{i}"));
        command.CommandText = $"DELETE FROM notifications WHERE user_id = @uid AND id IN ({placeholders})";
        command.Parameters.AddWithValue("@uid", userId.ToString());
        for (int i = 0; i < ids.Length; i++)
            command.Parameters.AddWithValue($"@id{i}", ids[i]);
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAllAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM notifications WHERE user_id = @uid";
        command.Parameters.AddWithValue("@uid", userId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM notifications WHERE user_id = @uid AND is_read = 0";
        command.Parameters.AddWithValue("@uid", userId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public async Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE notifications SET is_read = 1 WHERE id = @id AND user_id = @uid";
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@uid", userId.ToString());
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task MarkAllAsReadAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE notifications SET is_read = 1 WHERE user_id = @uid AND is_read = 0";
        command.Parameters.AddWithValue("@uid", userId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<long>> GetUsersToNotifyAsync(int skpdId, long excludeUserId, CancellationToken ct = default)
    {
        var ids = new List<long>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT u.id
            FROM users u
            INNER JOIN user_roles ur ON u.id = ur.user_id
            INNER JOIN roles r ON ur.role_id = r.id
            WHERE r.name IN ('Admin', 'Editor')
              AND u.skpd_id = @skpdId
              AND u.id != @excludeUserId
              AND u.deleted_at IS NULL AND u.is_active = 1
            """;
        command.Parameters.AddWithValue("@skpdId", skpdId);
        command.Parameters.AddWithValue("@excludeUserId", excludeUserId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetInt64("id"));
        }

        logger.LogDebug(
            "GetUsersToNotifyAsync(skpdId={SkpdId}, exclude={ExcludeUserId}) → {Count} target(s).",
            skpdId, excludeUserId, ids.Count);

        return ids;
    }

    public Task<IReadOnlyList<long>> GetEditorsBySkpdAsync(int skpdId, long excludeUserId, CancellationToken ct = default)
        => GetUsersByPermissionInSkpdAsync("edit_berita", skpdId, excludeUserId, ct);

    public async Task<IReadOnlyList<long>> GetSkpdAdminsAsync(int skpdId, long excludeUserId, CancellationToken ct = default)
    {
        var ids = new List<long>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT u.id
            FROM users u
            INNER JOIN user_roles ur ON u.id = ur.user_id
            INNER JOIN roles r ON ur.role_id = r.id
            WHERE r.name = 'Admin'
              AND u.skpd_id = @skpdId
              AND u.id != @excludeUserId
              AND u.deleted_at IS NULL AND u.is_active = 1
            """;
        command.Parameters.AddWithValue("@skpdId", skpdId);
        command.Parameters.AddWithValue("@excludeUserId", excludeUserId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetInt64("id"));

        logger.LogDebug(
            "GetSkpdAdminsAsync(skpdId={SkpdId}, exclude={ExcludeUserId}) → {Count} admin(s).",
            skpdId, excludeUserId, ids.Count);

        return ids;
    }

    private async Task<IReadOnlyList<long>> GetUsersByPermissionInSkpdAsync(
        string permission, int skpdId, long excludeUserId, CancellationToken ct)
    {
        var ids = new List<long>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT u.id
            FROM users u
            INNER JOIN user_roles ur ON u.id = ur.user_id
            INNER JOIN roles r ON ur.role_id = r.id
            INNER JOIN role_permissions rp ON r.id = rp.role_id
            INNER JOIN permissions p ON rp.permission_id = p.id
            WHERE p.name = @permission
              AND u.skpd_id = @skpdId
              AND u.id != @excludeUserId
              AND u.deleted_at IS NULL AND u.is_active = 1
            """;
        command.Parameters.AddWithValue("@permission", permission);
        command.Parameters.AddWithValue("@skpdId", skpdId);
        command.Parameters.AddWithValue("@excludeUserId", excludeUserId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetInt64("id"));

        logger.LogDebug(
            "GetUsersByPermission(permission={Permission}, skpdId={SkpdId}, exclude={ExcludeUserId}) → {Count} target(s).",
            permission, skpdId, excludeUserId, ids.Count);

        return ids;
    }
}
