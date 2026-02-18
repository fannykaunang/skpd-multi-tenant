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
            command.CommandText = "SELECT id, name FROM tags WHERE name LIKE @search ORDER BY name ASC";
            command.Parameters.AddWithValue("@search", $"%{search}%");
        }
        else
        {
            command.CommandText = "SELECT id, name FROM tags ORDER BY name ASC";
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new Tag { Id = reader.GetInt32("id"), Name = reader.GetString("name") });
        }

        return items;
    }

    public async Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM tags WHERE id = @id LIMIT 1";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new Tag { Id = reader.GetInt32("id"), Name = reader.GetString("name") };
    }

    public async Task<Tag> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO tags (name) VALUES (@name); SELECT LAST_INSERT_ID();";
        command.Parameters.AddWithValue("@name", request.Name.Trim());

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, UpdateTagRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE tags SET name = @name WHERE id = @id";
        command.Parameters.AddWithValue("@name", request.Name.Trim());
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
}
