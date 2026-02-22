using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface IPageService
{
    Task<PageListResponse> GetAllAsync(PageQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<Page?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Page?> GetBySlugAsync(int skpdId, string slug, CancellationToken cancellationToken = default);
    Task<Page> CreateAsync(CreatePageRequest request, long userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class PageService(IMySqlConnectionFactory connectionFactory) : IPageService
{
    public async Task<PageListResponse> GetAllAsync(PageQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var whereClauses = new List<string> { "p.deleted_at IS NULL" };
        if (queryParams.SkpdId.HasValue)
            whereClauses.Add("p.skpd_id = @skpdId");
        if (!string.IsNullOrEmpty(queryParams.Status))
            whereClauses.Add("p.status = @status");
        if (!string.IsNullOrEmpty(queryParams.Search))
            whereClauses.Add("(p.title LIKE @search OR p.content LIKE @search)");

        var where = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        // Count Total
        var countQuery = $"SELECT COUNT(*) FROM pages p {where}";
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = countQuery;
        AddQueryParams(countCmd, queryParams);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));

        // Get Items
        var selectQuery = $@"
            SELECT p.*, u.username as created_by_name
            FROM pages p
            LEFT JOIN users u ON p.created_by = u.id
            {where}
            ORDER BY p.created_at DESC
            LIMIT @limit OFFSET @offset";

        await using var dataCmd = connection.CreateCommand();
        dataCmd.CommandText = selectQuery;
        AddQueryParams(dataCmd, queryParams);
        dataCmd.Parameters.AddWithValue("@limit", queryParams.PageSize);
        dataCmd.Parameters.AddWithValue("@offset", (queryParams.Page - 1) * queryParams.PageSize);

        var items = new List<Page>();
        await using var reader = await dataCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapPage(reader));
        }

        return new PageListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<Page?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.*, u.username as created_by_name
            FROM pages p
            LEFT JOIN users u ON p.created_by = u.id
            WHERE p.id = @id AND p.deleted_at IS NULL";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapPage(reader);
        }
        return null;
    }

    public async Task<Page?> GetBySlugAsync(int skpdId, string slug, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.*, u.nama as created_by_name
            FROM pages p
            LEFT JOIN users u ON p.created_by = u.id
            WHERE p.skpd_id = @skpdId AND p.slug = @slug AND p.deleted_at IS NULL";
        cmd.Parameters.AddWithValue("@skpdId", skpdId);
        cmd.Parameters.AddWithValue("@slug", slug);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapPage(reader);
        }
        return null;
    }

    public async Task<Page> CreateAsync(CreatePageRequest request, long userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO pages (skpd_id, title, slug, content, status, created_by, created_at)
            VALUES (@skpdId, @title, @slug, @content, @status, @createdBy, NOW());
            SELECT LAST_INSERT_ID();";

        cmd.Parameters.AddWithValue("@skpdId", request.SkpdId);
        cmd.Parameters.AddWithValue("@title", request.Title ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@slug", request.Slug ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@content", request.Content ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", request.Status ?? "draft");
        cmd.Parameters.AddWithValue("@createdBy", userId);

        var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return (await GetByIdAsync(id, cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE pages
            SET title = @title,
                slug = @slug,
                content = @content,
                status = @status,
                updated_at = NOW()
            WHERE id = @id AND deleted_at IS NULL";

        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@title", request.Title ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@slug", request.Slug ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@content", request.Content ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", request.Status ?? "draft");

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE pages SET deleted_at = NOW() WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static void AddQueryParams(MySqlCommand cmd, PageQueryParams queryParams)
    {
        if (queryParams.SkpdId.HasValue)
            cmd.Parameters.AddWithValue("@skpdId", queryParams.SkpdId.Value);
        if (!string.IsNullOrEmpty(queryParams.Status))
            cmd.Parameters.AddWithValue("@status", queryParams.Status);
        if (!string.IsNullOrEmpty(queryParams.Search))
            cmd.Parameters.AddWithValue("@search", $"%{queryParams.Search}%");
    }

    private static Page MapPage(MySqlDataReader reader)
    {
        return new Page
        {
            Id = reader.GetInt64("id"),
            SkpdId = reader.GetInt32("skpd_id"),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString("title"),
            Slug = reader.IsDBNull(reader.GetOrdinal("slug")) ? null : reader.GetString("slug"),
            Content = reader.IsDBNull(reader.GetOrdinal("content")) ? null : reader.GetString("content"),
            Status = reader.GetString("status"),
            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64("created_by"),
            CreatedByName = reader.IsDBNull(reader.GetOrdinal("created_by_name")) ? null : reader.GetString("created_by_name"),
            CreatedAt = reader.GetDateTime("created_at"),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at"),
            DeletedAt = reader.IsDBNull(reader.GetOrdinal("deleted_at")) ? null : reader.GetDateTime("deleted_at")
        };
    }
}
