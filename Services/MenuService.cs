using System.Data;
using System.Text;
using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

// ── Interface ──────────────────────────────────────────────────────────────────

public interface IMenuService
{
    // Menu list + CRUD
    Task<IReadOnlyList<MenuSummary>> GetAllAsync(int? skpdId, CancellationToken ct = default);
    Task<MenuResponse?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MenuResponse> CreateMenuAsync(int skpdId, CreateMenuRequest request, CancellationToken ct = default);
    Task<bool> UpdateMenuAsync(int id, UpdateMenuRequest request, CancellationToken ct = default);
    Task<bool> DeleteMenuAsync(int id, CancellationToken ct = default);

    // Menu item CRUD
    Task<MenuItemFlat> CreateMenuItemAsync(int menuId, CreateMenuItemRequest request, CancellationToken ct = default);
    Task<bool> UpdateMenuItemAsync(int id, UpdateMenuItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteMenuItemAsync(int id, CancellationToken ct = default);

    // Bulk reorder with transaction
    Task<MenuResponse?> ReorderItemsAsync(int menuId, IReadOnlyList<ReorderItem> items, CancellationToken ct = default);

    // Security helpers (used by endpoints for tenant validation)
    Task<int?> GetMenuSkpdIdAsync(int menuId, CancellationToken ct = default);
    Task<MenuItemFlat?> GetMenuItemAsync(int id, CancellationToken ct = default);
}

// ── Implementation ─────────────────────────────────────────────────────────────

public sealed class MenuService(IMySqlConnectionFactory connectionFactory) : IMenuService
{
    private const int MaxDepth = 5; // levels 1–5 (depth 0–4)

    // ════════════════════════════════════════════════════════════════════════
    // MENU LIST
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<MenuSummary>> GetAllAsync(
        int? skpdId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = skpdId.HasValue
            ? """
              SELECT m.id, m.skpd_id, s.nama AS skpd_nama, m.name, m.created_at,
                     COUNT(mi.id) AS item_count
              FROM menus m
              LEFT JOIN skpd s  ON s.id  = m.skpd_id
              LEFT JOIN menu_items mi ON mi.menu_id = m.id
              WHERE m.skpd_id = @skpdId
              GROUP BY m.id, m.skpd_id, s.nama, m.name, m.created_at
              ORDER BY m.created_at DESC
              """
            : """
              SELECT m.id, m.skpd_id, s.nama AS skpd_nama, m.name, m.created_at,
                     COUNT(mi.id) AS item_count
              FROM menus m
              LEFT JOIN skpd s  ON s.id  = m.skpd_id
              LEFT JOIN menu_items mi ON mi.menu_id = m.id
              GROUP BY m.id, m.skpd_id, s.nama, m.name, m.created_at
              ORDER BY m.created_at DESC
              """;

        if (skpdId.HasValue)
            cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);

        var result = new List<MenuSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new MenuSummary
            {
                Id = reader.GetInt32("id"),
                SkpdId = reader.GetInt32("skpd_id"),
                SkpdNama = reader.IsDBNull("skpd_nama") ? string.Empty : reader.GetString("skpd_nama"),
                Name = reader.GetString("name"),
                CreatedAt = reader.GetDateTime("created_at"),
                ItemCount = reader.GetInt32("item_count"),
            });
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════════
    // MENU CRUD
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the menu with all its items built as a nested tree.
    /// Only two queries are issued: one for the menu header, one for all items.
    /// </summary>
    public async Task<MenuResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await GetByIdInternalAsync(connection, id, ct);
    }

    public async Task<MenuResponse> CreateMenuAsync(int skpdId, CreateMenuRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO menus (skpd_id, name)
            VALUES (@skpdId, @name);
            SELECT LAST_INSERT_ID();
            """;
        cmd.Parameters.AddWithValue("@skpdId", skpdId);
        cmd.Parameters.AddWithValue("@name", request.Name.Trim());

        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return (await GetByIdInternalAsync(connection, newId, ct))!;
    }

    public async Task<bool> UpdateMenuAsync(int id, UpdateMenuRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "UPDATE menus SET name = @name WHERE id = @id";
        cmd.Parameters.AddWithValue("@name", request.Name.Trim());
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>Deletes the menu; FK ON DELETE CASCADE removes all menu_items.</summary>
    public async Task<bool> DeleteMenuAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM menus WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    // MENU ITEM CRUD
    // ════════════════════════════════════════════════════════════════════════

    public async Task<MenuItemFlat> CreateMenuItemAsync(
        int menuId, CreateMenuItemRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Load all existing items once for validation
        var existing = await GetFlatItemsAsync(connection, menuId, ct);
        var parentMap = existing.ToDictionary(i => i.Id, i => i.ParentId);

        if (request.ParentId.HasValue)
        {
            // Parent must belong to the same menu
            if (!parentMap.ContainsKey(request.ParentId.Value))
                throw new InvalidOperationException(
                    $"Parent item {request.ParentId} tidak ditemukan dalam menu ini.");

            // New node will be one level deeper than its parent
            var newDepth = GetDepthFromMap(request.ParentId.Value, parentMap) + 1;
            if (newDepth >= MaxDepth)
                throw new InvalidOperationException(
                    $"Kedalaman menu melebihi batas maksimal {MaxDepth} level.");
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO menu_items (menu_id, parent_id, title, url, sort_order, is_active)
            VALUES (@menuId, @parentId, @title, @url, @sortOrder, @isActive);
            SELECT LAST_INSERT_ID();
            """;
        cmd.Parameters.AddWithValue("@menuId", menuId);
        cmd.Parameters.AddWithValue("@parentId", request.ParentId.HasValue ? request.ParentId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@title", request.Title.Trim());
        cmd.Parameters.AddWithValue("@url", request.Url.Trim());
        cmd.Parameters.AddWithValue("@sortOrder", request.SortOrder);
        cmd.Parameters.AddWithValue("@isActive", request.IsActive ? 1 : 0);

        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return (await GetMenuItemAsync(connection, newId, ct))!;
    }

    public async Task<bool> UpdateMenuItemAsync(
        int id, UpdateMenuItemRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Fetch item to know its menu
        var item = await GetMenuItemAsync(connection, id, ct);
        if (item is null) return false;

        // Load all sibling items for validation (one query)
        var existing = await GetFlatItemsAsync(connection, item.MenuId, ct);
        var parentMap = existing.ToDictionary(i => i.Id, i => i.ParentId);

        // Cannot be its own parent
        if (request.ParentId == id)
            throw new InvalidOperationException(
                "Menu item tidak boleh menjadi parent dirinya sendiri.");

        if (request.ParentId.HasValue)
        {
            // Parent must belong to the same menu
            if (!parentMap.ContainsKey(request.ParentId.Value))
                throw new InvalidOperationException(
                    $"Parent item {request.ParentId} tidak ditemukan dalam menu yang sama.");
        }

        // Simulate the new state in the parent map
        parentMap[id] = request.ParentId;

        // Circular reference check (traverse upward from the new parent)
        if (request.ParentId.HasValue &&
            WouldCreateCycle(id, request.ParentId.Value, parentMap))
            throw new InvalidOperationException(
                "Perubahan parent akan menyebabkan circular reference.");

        // Depth check — verify ALL nodes after the proposed change
        foreach (var node in existing)
        {
            if (GetDepthFromMap(node.Id, parentMap) >= MaxDepth)
                throw new InvalidOperationException(
                    $"Perubahan parent menyebabkan menu item {node.Id} " +
                    $"melebihi kedalaman maksimal {MaxDepth} level.");
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE menu_items
            SET parent_id  = @parentId,
                title      = @title,
                url        = @url,
                sort_order = @sortOrder,
                is_active  = @isActive
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@parentId", request.ParentId.HasValue ? request.ParentId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@title", request.Title.Trim());
        cmd.Parameters.AddWithValue("@url", request.Url.Trim());
        cmd.Parameters.AddWithValue("@sortOrder", request.SortOrder);
        cmd.Parameters.AddWithValue("@isActive", request.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>Deletes the item; FK ON DELETE CASCADE removes all descendants.</summary>
    public async Task<bool> DeleteMenuItemAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM menu_items WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    // BULK REORDER  (one round-trip UPDATE via CASE-WHEN + transaction)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<MenuResponse?> ReorderItemsAsync(
        int menuId, IReadOnlyList<ReorderItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0)
            return await GetByIdAsync(menuId, ct);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // ── 1. Load all existing items for this menu ───────────────────────
        var existing = await GetFlatItemsAsync(connection, menuId, ct);
        var existingIds = existing.Select(e => e.Id).ToHashSet();

        // ── 2. Validate request IDs ────────────────────────────────────────
        foreach (var item in items)
        {
            if (!existingIds.Contains(item.Id))
                throw new InvalidOperationException(
                    $"Menu item {item.Id} tidak ditemukan dalam menu {menuId}.");

            if (item.ParentId == item.Id)
                throw new InvalidOperationException(
                    $"Menu item {item.Id} tidak boleh menjadi parent dirinya sendiri.");

            if (item.ParentId.HasValue && !existingIds.Contains(item.ParentId.Value))
                throw new InvalidOperationException(
                    $"Parent {item.ParentId} tidak ditemukan dalam menu {menuId}.");
        }

        // ── 3. Build merged parent map (existing overridden by request) ────
        var parentMap = existing.ToDictionary(i => i.Id, i => i.ParentId);
        foreach (var item in items)
            parentMap[item.Id] = item.ParentId;

        // ── 4. Validate cycles and depth across ALL nodes ──────────────────
        foreach (var item in items)
        {
            if (WouldCreateCycle(item.Id, item.ParentId, parentMap))
                throw new InvalidOperationException(
                    $"Circular reference terdeteksi pada menu item {item.Id}.");
        }

        foreach (var node in existing)
        {
            if (GetDepthFromMap(node.Id, parentMap) >= MaxDepth)
                throw new InvalidOperationException(
                    $"Menu item {node.Id} melebihi kedalaman maksimal {MaxDepth} level setelah reorder.");
        }

        // ── 5. Single bulk UPDATE inside a transaction ─────────────────────
        // Builds:
        //   UPDATE menu_items
        //   SET parent_id  = CASE id WHEN @id0 THEN @pid0 WHEN @id1 THEN @pid1 … ELSE parent_id END,
        //       sort_order = CASE id WHEN @id0 THEN @so0  WHEN @id1 THEN @so1  … ELSE sort_order END
        //   WHERE menu_id = @menuId AND id IN (@id0, @id1, …)
        var sb = new StringBuilder();
        sb.Append("UPDATE menu_items SET parent_id = CASE id");
        for (var i = 0; i < items.Count; i++)
            sb.Append($" WHEN @id{i} THEN @pid{i}");
        sb.Append(" ELSE parent_id END, sort_order = CASE id");
        for (var i = 0; i < items.Count; i++)
            sb.Append($" WHEN @id{i} THEN @so{i}");
        sb.Append(" ELSE sort_order END WHERE menu_id = @menuId AND id IN (");
        sb.Append(string.Join(", ", Enumerable.Range(0, items.Count).Select(i => $"@id{i}")));
        sb.Append(')');

        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sb.ToString();
            cmd.Parameters.AddWithValue("@menuId", menuId);

            for (var i = 0; i < items.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@id{i}", items[i].Id);
                cmd.Parameters.AddWithValue($"@pid{i}", items[i].ParentId.HasValue
                    ? items[i].ParentId.Value
                    : DBNull.Value);
                cmd.Parameters.AddWithValue($"@so{i}", items[i].SortOrder);
            }

            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // ── 6. Return the updated tree (reuses the same connection) ────────
        return await GetByIdInternalAsync(connection, menuId, ct);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SECURITY HELPERS  (used by endpoints)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<int?> GetMenuSkpdIdAsync(int menuId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT skpd_id FROM menus WHERE id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", menuId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    /// <summary>Public overload — opens its own connection.</summary>
    public async Task<MenuItemFlat?> GetMenuItemAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await GetMenuItemAsync(connection, id, ct);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fetches menu header + all items in two sequential queries on the same
    /// connection, then builds the nested tree in memory (no recursive SQL).
    /// </summary>
    private static async Task<MenuResponse?> GetByIdInternalAsync(
        MySqlConnection connection, int id, CancellationToken ct)
    {
        // ── query 1: menu header ──────────────────────────────────────────
        MenuResponse? menu;
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, skpd_id, name, created_at
                FROM menus
                WHERE id = @id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            menu = new MenuResponse
            {
                Id = reader.GetInt32("id"),
                SkpdId = reader.GetInt32("skpd_id"),
                Name = reader.GetString("name"),
                CreatedAt = reader.GetDateTime("created_at"),
            };
        } // reader disposed here — connection is free for next command

        // ── query 2: all menu_items (one query, tree built in C#) ─────────
        var flatItems = await GetFlatItemsAsync(connection, id, ct);
        menu.Items = BuildTree(flatItems);
        return menu;
    }

    /// <summary>
    /// Fetches all menu_items for a given menu in a single SELECT, ordered
    /// by sort_order so the tree builder can rely on stable ordering.
    /// </summary>
    private static async Task<List<MenuItemFlat>> GetFlatItemsAsync(
        MySqlConnection connection, int menuId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, menu_id, parent_id, title, url, sort_order, is_active
            FROM menu_items
            WHERE menu_id = @menuId
            ORDER BY sort_order, id
            """;
        cmd.Parameters.AddWithValue("@menuId", menuId);

        var items = new List<MenuItemFlat>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(MapFlat(reader));

        return items;
    }

    private static async Task<MenuItemFlat?> GetMenuItemAsync(
        MySqlConnection connection, int id, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, menu_id, parent_id, title, url, sort_order, is_active
            FROM menu_items
            WHERE id = @id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapFlat(reader) : null;
    }

    // ── Tree builder ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a nested tree from a flat list using a dictionary for O(n) lookup.
    /// Items with an unknown parent are treated as roots.
    /// Each level is sorted by sort_order.
    /// </summary>
    private static List<MenuItemNode> BuildTree(IReadOnlyList<MenuItemFlat> items)
    {
        var dict = new Dictionary<int, MenuItemNode>(items.Count);
        foreach (var item in items)
        {
            dict[item.Id] = new MenuItemNode
            {
                Id = item.Id,
                ParentId = item.ParentId,
                Title = item.Title,
                Url = item.Url,
                SortOrder = item.SortOrder,
                IsActive = item.IsActive,
            };
        }

        var roots = new List<MenuItemNode>();
        foreach (var item in items)
        {
            var node = dict[item.Id];
            if (item.ParentId is null || !dict.ContainsKey(item.ParentId.Value))
                roots.Add(node);
            else
                dict[item.ParentId.Value].Children.Add(node);
        }

        SortTree(roots);
        return roots;
    }

    private static void SortTree(List<MenuItemNode> nodes)
    {
        nodes.Sort(static (a, b) => a.SortOrder.CompareTo(b.SortOrder));
        foreach (var node in nodes)
            if (node.Children.Count > 0)
                SortTree(node.Children);
    }

    // ── Validation helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true if setting <paramref name="nodeId"/>'s parent to
    /// <paramref name="candidateParentId"/> would form a cycle in the
    /// supplied <paramref name="parentMap"/>.
    /// Traverses upward from the candidate parent; if we reach <paramref name="nodeId"/>
    /// before hitting a root or an unknown node, a cycle exists.
    /// </summary>
    private static bool WouldCreateCycle(
        int nodeId, int? candidateParentId, Dictionary<int, int?> parentMap)
    {
        if (candidateParentId is null) return false;

        var visited = new HashSet<int>();
        var current = candidateParentId;

        while (current is not null)
        {
            if (current.Value == nodeId) return true;
            if (!visited.Add(current.Value)) break; // unexpected cycle in existing data
            parentMap.TryGetValue(current.Value, out current);
        }

        return false;
    }

    /// <summary>
    /// Returns the depth of <paramref name="nodeId"/> within the tree defined by
    /// <paramref name="parentMap"/>.  Root nodes (parentId == null or unknown) = depth 0.
    /// </summary>
    private static int GetDepthFromMap(int nodeId, Dictionary<int, int?> parentMap)
    {
        var depth = 0;
        var visited = new HashSet<int>();

        parentMap.TryGetValue(nodeId, out var current);
        while (current is not null)
        {
            depth++;
            if (!visited.Add(current.Value)) break; // cycle guard
            parentMap.TryGetValue(current.Value, out current);
        }

        return depth;
    }

    // ── Row mapper ─────────────────────────────────────────────────────────

    private static MenuItemFlat MapFlat(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt32("id"),
        MenuId = reader.GetInt32("menu_id"),
        ParentId = reader.IsDBNull("parent_id") ? null : reader.GetInt32("parent_id"),
        Title = reader.GetString("title"),
        Url = reader.GetString("url"),
        SortOrder = reader.GetInt32("sort_order"),
        IsActive = reader.GetBoolean("is_active"),
    };
}
