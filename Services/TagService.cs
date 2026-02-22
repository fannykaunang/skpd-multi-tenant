using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ITagService
{
    Task<IReadOnlyList<Tag>> GetAllAsync(string? search, CancellationToken ct = default);
    Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Tag> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateTagRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task SetBeritaTagsAsync(long beritaId, List<int> tagIds, CancellationToken ct = default);
    Task<IReadOnlyList<Berita>> GetBeritaByTagAsync(int tagId, CancellationToken ct = default);
}

public sealed class TagService(IMySqlConnectionFactory connectionFactory) : ITagService
{
    public async Task<IReadOnlyList<Tag>> GetAllAsync(string? search, CancellationToken ct = default)
    {
        var items = new List<Tag>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(search))
        {
            command.CommandText = @"
                SELECT t.id, t.name, t.slug, t.created_at, COUNT(b.id) as usage_count 
                FROM tags t 
                LEFT JOIN berita_tags bt ON t.id = bt.tag_id 
                LEFT JOIN berita b ON bt.berita_id = b.id AND b.deleted_at IS NULL
                WHERE t.name LIKE @search 
                GROUP BY t.id, t.name, t.slug, t.created_at
                ORDER BY t.id ASC";
            command.Parameters.AddWithValue("@search", $"%{search}%");
        }
        else
        {
            command.CommandText = @"
                SELECT t.id, t.name, t.slug, t.created_at, COUNT(b.id) as usage_count 
                FROM tags t 
                LEFT JOIN berita_tags bt ON t.id = bt.tag_id 
                LEFT JOIN berita b ON bt.berita_id = b.id AND b.deleted_at IS NULL
                GROUP BY t.id, t.name, t.slug, t.created_at
                ORDER BY t.ID ASC";
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new Tag { 
                Id = reader.GetInt32("id"), 
                Name = reader.GetString("name"),
                Slug = reader.GetString("slug"),
                CreatedAt = reader.GetDateTime("created_at"),
                UsageCount = Convert.ToInt32(reader["usage_count"])
            });
        }

        return items;
    }

    public async Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, slug, created_at FROM tags WHERE id = @id LIMIT 1";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new Tag 
        { 
            Id = reader.GetInt32("id"), 
            Name = reader.GetString("name"),
            Slug = reader.GetString("slug"),
            CreatedAt = reader.GetDateTime("created_at"),
        };
    }

    public async Task<Tag> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        
        var name = request.Name.Trim();
        var slug = !string.IsNullOrWhiteSpace(request.Slug) ? request.Slug.Trim() : name.ToLower().Replace(" ", "-");
        
        command.CommandText = "INSERT INTO tags (name, slug, created_at) VALUES (@name, @slug, UTC_TIMESTAMP()); SELECT LAST_INSERT_ID();";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@slug", slug);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, UpdateTagRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        
        var name = request.Name.Trim();
        var slug = !string.IsNullOrWhiteSpace(request.Slug) ? request.Slug.Trim() : name.ToLower().Replace(" ", "-");
        
        command.CommandText = "UPDATE tags SET name = @name, slug = @slug WHERE id = @id";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@slug", slug);
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tags WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task SetBeritaTagsAsync(long beritaId, List<int> tagIds, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM berita_tags WHERE berita_id = @beritaId";
        deleteCmd.Parameters.AddWithValue("@beritaId", beritaId);
        await deleteCmd.ExecuteNonQueryAsync(ct);

        foreach (var tagId in tagIds)
        {
            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT IGNORE INTO berita_tags (berita_id, tag_id) VALUES (@b, @t)";
            insertCmd.Parameters.AddWithValue("@b", beritaId);
            insertCmd.Parameters.AddWithValue("@t", tagId);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Berita>> GetBeritaByTagAsync(int tagId, CancellationToken ct = default)
    {
        var items = new List<Berita>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT b.id, b.title, b.status, b.published_at, b.view_count
            FROM berita b
            INNER JOIN berita_tags bt ON b.id = bt.berita_id
            WHERE bt.tag_id = @tagId AND b.deleted_at IS NULL
            ORDER BY b.created_at DESC";
        command.Parameters.AddWithValue("@tagId", tagId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var ordinalPublishedAt = reader.GetOrdinal("published_at");
            items.Add(new Berita
            {
                Id = reader.GetInt64("id"),
                Title = reader.GetString("title"),
                Status = reader.GetString("status"),
                PublishedAt = reader.IsDBNull(ordinalPublishedAt) ? null : reader.GetDateTime(ordinalPublishedAt),
                ViewCount = reader.GetInt64("view_count")
            });
        }
        return items;
    }
}
