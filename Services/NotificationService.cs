using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface INotificationService
{
    Task CreateAsync(string userId, string title, string message, string? link, string type, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(long userId, int limit, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(long userId, CancellationToken ct = default);
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
