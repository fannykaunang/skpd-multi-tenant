using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface IPenggunaService
{
    Task<IReadOnlyList<Pengguna>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pengguna>> GetAllBySkpdAsync(int skpdId, CancellationToken cancellationToken = default);
    Task<Pengguna?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Pengguna> CreateAsync(CreatePenggunaRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(long id, UpdatePenggunaRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleItem>> GetAllRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleItem>> GetRolesBySkpdAsync(int skpdId, CancellationToken cancellationToken = default);
    /// <summary>Verify old password then update to new. Returns false if old password is wrong.</summary>
    Task<bool> ChangePasswordAsync(long userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default);
    /// <summary>Fetch live permissions from DB (not from JWT). Used by /auth/me so role changes take effect immediately.</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(long userId, int? skpdId, CancellationToken cancellationToken = default);
}

public sealed class PenggunaService(IMySqlConnectionFactory connectionFactory) : IPenggunaService
{
    public async Task<IReadOnlyList<Pengguna>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await FetchPenggunaAsync(null, null, cancellationToken);
    }

    public async Task<IReadOnlyList<Pengguna>> GetAllBySkpdAsync(int skpdId, CancellationToken cancellationToken = default)
    {
        return await FetchPenggunaAsync(skpdId, null, cancellationToken);
    }

    public async Task<Pengguna?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var list = await FetchPenggunaAsync(null, id, cancellationToken);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<Pengguna> CreateAsync(CreatePenggunaRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);

        // Insert user
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO users (skpd_id, username, email, password_hash, is_active)
            VALUES (@skpdId, @username, @email, @passwordHash, @isActive);
            SELECT LAST_INSERT_ID();";
        command.Parameters.AddWithValue("@skpdId", request.SkpdId.HasValue ? request.SkpdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@username", request.Username);
        command.Parameters.AddWithValue("@email", request.Email);
        command.Parameters.AddWithValue("@passwordHash", passwordHash);
        command.Parameters.AddWithValue("@isActive", request.IsActive);

        var userId = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));

        // Assign roles
        if (request.RoleIds.Count > 0)
        {
            await AssignRolesAsync(connection, userId, request.RoleIds, cancellationToken);
        }

        return (await GetByIdAsync(userId, cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(long id, UpdatePenggunaRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Build update query
        var setClauses = new List<string>
        {
            "skpd_id = @skpdId",
            "email = @email",
            "is_active = @isActive",
            "updated_at = UTC_TIMESTAMP()"
        };

        await using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@skpdId", request.SkpdId.HasValue ? request.SkpdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@email", request.Email);
        command.Parameters.AddWithValue("@isActive", request.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);
            setClauses.Add("password_hash = @passwordHash");
            command.Parameters.AddWithValue("@passwordHash", passwordHash);
        }

        command.CommandText = $@"
            UPDATE users
            SET {string.Join(", ", setClauses)}
            WHERE id = @id AND deleted_at IS NULL";

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0) return false;

        // Update roles: delete existing, insert new
        await using var deleteRolesCmd = connection.CreateCommand();
        deleteRolesCmd.CommandText = "DELETE FROM user_roles WHERE user_id = @userId";
        deleteRolesCmd.Parameters.AddWithValue("@userId", id);
        await deleteRolesCmd.ExecuteNonQueryAsync(cancellationToken);

        if (request.RoleIds.Count > 0)
        {
            await AssignRolesAsync(connection, id, request.RoleIds, cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE users
            SET deleted_at = UTC_TIMESTAMP(), is_active = 0
            WHERE id = @id AND deleted_at IS NULL";
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> ChangePasswordAsync(long userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Fetch current hash
        await using var fetchCmd = connection.CreateCommand();
        fetchCmd.CommandText = "SELECT password_hash FROM users WHERE id = @id AND deleted_at IS NULL LIMIT 1";
        fetchCmd.Parameters.AddWithValue("@id", userId);
        var hash = (string?)await fetchCmd.ExecuteScalarAsync(cancellationToken);

        if (hash is null || !BCrypt.Net.BCrypt.Verify(oldPassword, hash))
            return false;

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE users SET password_hash = @hash, updated_at = UTC_TIMESTAMP() WHERE id = @id";
        updateCmd.Parameters.AddWithValue("@hash", newHash);
        updateCmd.Parameters.AddWithValue("@id", userId);
        await updateCmd.ExecuteNonQueryAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<RoleItem>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return await FetchRolesAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleItem>> GetRolesBySkpdAsync(int skpdId, CancellationToken cancellationToken = default)
    {
        return await FetchRolesAsync(skpdId, cancellationToken);
    }

    private async Task<IReadOnlyList<Pengguna>> FetchPenggunaAsync(int? skpdId, long? userId, CancellationToken cancellationToken)
    {
        var dict = new Dictionary<long, Pengguna>();

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var whereClauses = new List<string> { "u.deleted_at IS NULL" };

        if (skpdId.HasValue)
        {
            whereClauses.Add("u.skpd_id = @skpdId");
            command.Parameters.AddWithValue("@skpdId", skpdId.Value);
        }

        if (userId.HasValue)
        {
            whereClauses.Add("u.id = @userId");
            command.Parameters.AddWithValue("@userId", userId.Value);
        }

        command.CommandText = $@"
            SELECT u.id, u.skpd_id, s.nama AS skpd_nama,
                   u.username, u.email, u.is_active,
                   u.last_login, u.created_at, u.updated_at,
                   r.id AS role_id, r.name AS role_name
            FROM users u
            LEFT JOIN skpd s ON s.id = u.skpd_id
            LEFT JOIN user_roles ur ON ur.user_id = u.id
            LEFT JOIN roles r ON r.id = ur.role_id
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY u.id DESC";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64("id");

            if (!dict.TryGetValue(id, out var pengguna))
            {
                pengguna = new Pengguna
                {
                    Id = id,
                    SkpdId = reader.IsDBNull(reader.GetOrdinal("skpd_id")) ? null : reader.GetInt32("skpd_id"),
                    SkpdNama = reader.IsDBNull(reader.GetOrdinal("skpd_nama")) ? null : reader.GetString("skpd_nama"),
                    Username = reader.GetString("username"),
                    Email = reader.GetString("email"),
                    IsActive = reader.GetBoolean("is_active"),
                    LastLogin = reader.IsDBNull(reader.GetOrdinal("last_login")) ? null : reader.GetDateTime("last_login"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at")
                };
                dict[id] = pengguna;
            }

            if (!reader.IsDBNull(reader.GetOrdinal("role_id")))
            {
                pengguna.Roles.Add(new PenggunaRole
                {
                    Id = reader.GetInt32("role_id"),
                    Name = reader.GetString("role_name")
                });
            }
        }

        return dict.Values.ToList();
    }

    private async Task<IReadOnlyList<RoleItem>> FetchRolesAsync(int? skpdId, CancellationToken cancellationToken)
    {
        var items = new List<RoleItem>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        if (skpdId.HasValue)
        {
            // Operator SKPD: hanya roles milik SKPD-nya (exclude SuperAdmin role yang skpd_id = NULL)
            command.CommandText = @"
                SELECT id, skpd_id, name, description
                FROM roles
                WHERE skpd_id = @skpdId
                ORDER BY id";
            command.Parameters.AddWithValue("@skpdId", skpdId.Value);
        }
        else
        {
            // SuperAdmin: semua roles
            command.CommandText = @"
                SELECT id, skpd_id, name, description
                FROM roles
                ORDER BY id";
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new RoleItem
            {
                Id = reader.GetInt32("id"),
                SkpdId = reader.IsDBNull(reader.GetOrdinal("skpd_id")) ? null : reader.GetInt32("skpd_id"),
                Name = reader.GetString("name"),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString("description")
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(long userId, int? skpdId, CancellationToken cancellationToken = default)
    {
        var permissions = new List<string>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT p.name
            FROM permissions p
            JOIN role_permissions rp ON rp.permission_id = p.id
            JOIN roles r ON r.id = rp.role_id
            JOIN user_roles ur ON ur.role_id = r.id
            WHERE ur.user_id = @userId
              AND (r.skpd_id = @skpdId OR r.skpd_id IS NULL)
            """;
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@skpdId", (object?)skpdId ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            permissions.Add(reader.GetString("name"));

        return permissions;
    }

    private static async Task AssignRolesAsync(MySqlConnection connection, long userId, List<int> roleIds, CancellationToken cancellationToken)
    {
        foreach (var roleId in roleIds)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO user_roles (user_id, role_id) VALUES (@userId, @roleId)";
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@roleId", roleId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
