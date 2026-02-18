using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDetail>> GetAllAsync(int? skpdId, CancellationToken ct = default);
    Task<RoleDetail?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionItem>> GetAllPermissionsAsync(CancellationToken ct = default);
    Task<RoleDetail> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken ct = default);
    Task<bool> UpdatePermissionsAsync(int id, List<int> permissionIds, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class RoleService(IMySqlConnectionFactory connectionFactory) : IRoleService
{
    public async Task<IReadOnlyList<RoleDetail>> GetAllAsync(int? skpdId, CancellationToken ct = default)
    {
        return await FetchRolesAsync(skpdId: skpdId, roleId: null, ct);
    }

    public async Task<RoleDetail?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var list = await FetchRolesAsync(skpdId: null, roleId: id, ct);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<IReadOnlyList<PermissionItem>> GetAllPermissionsAsync(CancellationToken ct = default)
    {
        var items = new List<PermissionItem>();

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, description FROM permissions ORDER BY id";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new PermissionItem
            {
                Id = reader.GetInt32("id"),
                Name = reader.GetString("name"),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString("description")
            });
        }

        return items;
    }

    public async Task<RoleDetail> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Insert role
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO roles (skpd_id, name, description, created_at)
            VALUES (@skpdId, @name, @description, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";
        cmd.Parameters.AddWithValue("@skpdId", request.SkpdId.HasValue ? request.SkpdId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@name", request.Name.Trim());
        cmd.Parameters.AddWithValue("@description", (object?)request.Description?.Trim() ?? DBNull.Value);

        var roleId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

        // Assign permissions
        if (request.PermissionIds.Count > 0)
        {
            await AssignPermissionsAsync(connection, roleId, request.PermissionIds, ct);
        }

        return (await GetByIdAsync(roleId, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE roles
            SET name = @name, description = @description
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", request.Name.Trim());
        cmd.Parameters.AddWithValue("@description", (object?)request.Description?.Trim() ?? DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> UpdatePermissionsAsync(int id, List<int> permissionIds, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Verify role exists
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM roles WHERE id = @id";
        checkCmd.Parameters.AddWithValue("@id", id);
        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct)) > 0;
        if (!exists) return false;

        // Delete existing permissions
        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM role_permissions WHERE role_id = @roleId";
        deleteCmd.Parameters.AddWithValue("@roleId", id);
        await deleteCmd.ExecuteNonQueryAsync(ct);

        // Insert new permissions
        if (permissionIds.Count > 0)
        {
            await AssignPermissionsAsync(connection, id, permissionIds, ct);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Check if users are assigned to this role
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM user_roles WHERE role_id = @id";
        checkCmd.Parameters.AddWithValue("@id", id);
        var userCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct));

        if (userCount > 0)
            throw new InvalidOperationException($"Role ini masih digunakan oleh {userCount} pengguna. Hapus atau ubah role pengguna terlebih dahulu.");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM roles WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<RoleDetail>> FetchRolesAsync(
        int? skpdId, int? roleId, CancellationToken ct)
    {
        var dict = new Dictionary<int, RoleDetail>();

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        var whereClauses = new List<string>();

        if (skpdId.HasValue)
        {
            whereClauses.Add("r.skpd_id = @skpdId");
            cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);
        }

        if (roleId.HasValue)
        {
            whereClauses.Add("r.id = @roleId");
            cmd.Parameters.AddWithValue("@roleId", roleId.Value);
        }

        var where = whereClauses.Count > 0
            ? "WHERE " + string.Join(" AND ", whereClauses)
            : string.Empty;

        cmd.CommandText = $@"
            SELECT r.id, r.skpd_id, s.nama AS skpd_nama, r.name, r.description, r.created_at,
                   (SELECT COUNT(*) FROM user_roles ur WHERE ur.role_id = r.id) AS user_count,
                   p.id AS perm_id, p.name AS perm_name, p.description AS perm_desc
            FROM roles r
            LEFT JOIN skpd s ON s.id = r.skpd_id
            LEFT JOIN role_permissions rp ON rp.role_id = r.id
            LEFT JOIN permissions p ON p.id = rp.permission_id
            {where}
            ORDER BY r.id, p.id";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt32("id");

            if (!dict.TryGetValue(id, out var role))
            {
                role = new RoleDetail
                {
                    Id = id,
                    SkpdId = reader.IsDBNull(reader.GetOrdinal("skpd_id")) ? null : reader.GetInt32("skpd_id"),
                    SkpdNama = reader.IsDBNull(reader.GetOrdinal("skpd_nama")) ? null : reader.GetString("skpd_nama"),
                    Name = reader.GetString("name"),
                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString("description"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    UserCount = reader.GetInt32("user_count")
                };
                dict[id] = role;
            }

            if (!reader.IsDBNull(reader.GetOrdinal("perm_id")))
            {
                role.Permissions.Add(new PermissionItem
                {
                    Id = reader.GetInt32("perm_id"),
                    Name = reader.GetString("perm_name"),
                    Description = reader.IsDBNull(reader.GetOrdinal("perm_desc")) ? null : reader.GetString("perm_desc")
                });
            }
        }

        return dict.Values.ToList();
    }

    private static async Task AssignPermissionsAsync(
        MySqlConnection connection, int roleId, List<int> permissionIds, CancellationToken ct)
    {
        foreach (var permId in permissionIds)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT IGNORE INTO role_permissions (role_id, permission_id)
                VALUES (@roleId, @permId)";
            cmd.Parameters.AddWithValue("@roleId", roleId);
            cmd.Parameters.AddWithValue("@permId", permId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
