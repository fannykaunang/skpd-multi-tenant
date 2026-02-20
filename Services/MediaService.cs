using System.Data;
using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface IMediaService
{
    Task<IReadOnlyList<MediaItem>> GetAllAsync(int? skpdId, CancellationToken ct = default);
    Task<MediaItem?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<MediaItem> CreateAsync(MediaItem item, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class MediaService(IMySqlConnectionFactory connectionFactory) : IMediaService
{
    public async Task<IReadOnlyList<MediaItem>> GetAllAsync(int? skpdId, CancellationToken ct = default)
    {
        var items = new List<MediaItem>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();

        if (skpdId.HasValue)
        {
            command.CommandText = """
                SELECT m.id, m.skpd_id, s.nama AS skpd_nama,
                       m.uploaded_by, u.username AS uploaded_by_name,
                       m.file_name, m.title, m.slug, m.description,
                       m.file_path, m.file_type, m.file_size, m.created_at
                FROM media m
                LEFT JOIN skpd s ON s.id = m.skpd_id
                LEFT JOIN users u ON u.id = m.uploaded_by
                WHERE m.deleted_at IS NULL AND m.skpd_id = @skpdId
                ORDER BY m.created_at DESC
                """;
            command.Parameters.AddWithValue("@skpdId", skpdId.Value);
        }
        else
        {
            command.CommandText = """
                SELECT m.id, m.skpd_id, s.nama AS skpd_nama,
                       m.uploaded_by, u.username AS uploaded_by_name,
                       m.file_name, m.title, m.slug, m.description,
                       m.file_path, m.file_type, m.file_size, m.created_at
                FROM media m
                LEFT JOIN skpd s ON s.id = m.skpd_id
                LEFT JOIN users u ON u.id = m.uploaded_by
                WHERE m.deleted_at IS NULL
                ORDER BY m.created_at DESC
                """;
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(MapMedia(reader));

        return items;
    }

    public async Task<MediaItem?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.id, m.skpd_id, s.nama AS skpd_nama,
                   m.uploaded_by, u.username AS uploaded_by_name,
                   m.file_name, m.title, m.slug, m.description,
                   m.file_path, m.file_type, m.file_size, m.created_at
            FROM media m
            LEFT JOIN skpd s ON s.id = m.skpd_id
            LEFT JOIN users u ON u.id = m.uploaded_by
            WHERE m.id = @id AND m.deleted_at IS NULL
            LIMIT 1
            """;
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapMedia(reader) : null;
    }

    public async Task<MediaItem> CreateAsync(MediaItem item, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO media (skpd_id, uploaded_by, file_name, title, slug, description, file_path, file_type, file_size)
            VALUES (@skpdId, @uploadedBy, @fileName, @title, @slug, @description, @filePath, @fileType, @fileSize);
            SELECT LAST_INSERT_ID();
            """;
        command.Parameters.AddWithValue("@skpdId", item.SkpdId);
        command.Parameters.AddWithValue("@uploadedBy", item.UploadedBy.HasValue ? item.UploadedBy.Value : DBNull.Value);
        command.Parameters.AddWithValue("@fileName", item.FileName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@title", item.Title ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@slug", item.Slug ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@description", item.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@filePath", item.FilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@fileType", item.FileType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@fileSize", item.FileSize);

        var id = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE media SET deleted_at = UTC_TIMESTAMP() WHERE id = @id AND deleted_at IS NULL";
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private static MediaItem MapMedia(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64("id"),
        SkpdId = reader.GetInt32("skpd_id"),
        SkpdNama = reader.IsDBNull("skpd_nama") ? string.Empty : reader.GetString("skpd_nama"),
        UploadedBy = reader.IsDBNull("uploaded_by") ? null : reader.GetInt64("uploaded_by"),
        UploadedByName = reader.IsDBNull("uploaded_by_name") ? null : reader.GetString("uploaded_by_name"),
        FileName = reader.IsDBNull("file_name") ? null : reader.GetString("file_name"),
        Title = reader.IsDBNull("title") ? null : reader.GetString("title"),
        Slug = reader.IsDBNull("slug") ? null : reader.GetString("slug"),
        Description = reader.IsDBNull("description") ? null : reader.GetString("description"),
        FilePath = reader.IsDBNull("file_path") ? null : reader.GetString("file_path"),
        FileType = reader.IsDBNull("file_type") ? null : reader.GetString("file_type"),
        FileSize = reader.IsDBNull("file_size") ? 0 : reader.GetInt32("file_size"),
        CreatedAt = reader.GetDateTime("created_at"),
    };
}
