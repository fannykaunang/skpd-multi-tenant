using MySqlConnector;
using skpd_multi_tenant.Models;

namespace skpd_multi_tenant.Services;

public interface IBeritaService
{
    Task<IReadOnlyList<Berita>> GetAllAsync(BeritaQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<Berita?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Berita>> GetByCategorySlugAsync(int skpdId, string categorySlug, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<Berita> CreateAsync(CreateBeritaRequest request, long userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(long id, UpdateBeritaRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> IncrementViewCountAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class BeritaService(IMySqlConnectionFactory connectionFactory) : IBeritaService
{
    public async Task<IReadOnlyList<Berita>> GetAllAsync(BeritaQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var items = new List<Berita>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var whereConditions = new List<string> { "b.deleted_at IS NULL" };

        if (queryParams.SkpdId.HasValue)
        {
            whereConditions.Add("b.skpd_id = @skpdId");
            command.Parameters.AddWithValue("@skpdId", queryParams.SkpdId.Value);
        }

        if (queryParams.CategoryId.HasValue)
        {
            whereConditions.Add("b.category_id = @categoryId");
            command.Parameters.AddWithValue("@categoryId", queryParams.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            whereConditions.Add("b.status = @status");
            command.Parameters.AddWithValue("@status", queryParams.Status);
        }

        var whereClause = string.Join(" AND ", whereConditions);
        var offset = (queryParams.Page - 1) * queryParams.PageSize;

        command.CommandText = $@"SELECT b.id, b.skpd_id, s.nama as skpd_nama, b.category_id, c.name as category_name,
                                       b.title, b.slug, b.excerpt, b.content, b.thumbnail_url, b.status, 
                                       b.published_at, b.view_count, b.created_by, u.username as created_by_name, 
                                       b.created_at, b.updated_at
                                FROM berita b
                                LEFT JOIN skpd s ON b.skpd_id = s.id
                                LEFT JOIN users u ON b.created_by = u.id
                                LEFT JOIN categories c ON b.category_id = c.id
                                WHERE {whereClause}
                                ORDER BY b.created_at DESC
                                LIMIT @limit OFFSET @offset";

        command.Parameters.AddWithValue("@limit", queryParams.PageSize);
        command.Parameters.AddWithValue("@offset", offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapBerita(reader));
        }

        return items;
    }

    public async Task<Berita?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT b.id, b.skpd_id, s.nama as skpd_nama, b.category_id, c.name as category_name,
                                       b.title, b.slug, b.excerpt, b.content, b.thumbnail_url, b.status, 
                                       b.published_at, b.view_count, b.created_by, u.username as created_by_name, 
                                       b.created_at, b.updated_at
                                FROM berita b
                                LEFT JOIN skpd s ON b.skpd_id = s.id
                                LEFT JOIN users u ON b.created_by = u.id
                                LEFT JOIN categories c ON b.category_id = c.id
                                WHERE b.id = @id AND b.deleted_at IS NULL
                                LIMIT 1";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapBerita(reader);
    }

    public async Task<IReadOnlyList<Berita>> GetByCategorySlugAsync(int skpdId, string categorySlug, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var items = new List<Berita>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var offset = (page - 1) * pageSize;

        command.CommandText = @"SELECT b.id, b.skpd_id, s.nama as skpd_nama, b.category_id, c.name as category_name,
                                       b.title, b.slug, b.excerpt, b.content, b.thumbnail_url, b.status, 
                                       b.published_at, b.view_count, b.created_by, u.username as created_by_name, 
                                       b.created_at, b.updated_at
                                FROM berita b
                                LEFT JOIN skpd s ON b.skpd_id = s.id
                                LEFT JOIN users u ON b.created_by = u.id
                                INNER JOIN categories c ON b.category_id = c.id
                                WHERE b.skpd_id = @skpdId 
                                  AND c.slug = @categorySlug 
                                  AND b.deleted_at IS NULL
                                  AND b.status = 'published'
                                ORDER BY b.published_at DESC
                                LIMIT @limit OFFSET @offset";

        command.Parameters.AddWithValue("@skpdId", skpdId);
        command.Parameters.AddWithValue("@categorySlug", categorySlug);
        command.Parameters.AddWithValue("@limit", pageSize);
        command.Parameters.AddWithValue("@offset", offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapBerita(reader));
        }

        return items;
    }

    public async Task<Berita> CreateAsync(CreateBeritaRequest request, long userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Validate categoryId belongs to skpdId if categoryId is provided
        if (request.CategoryId.HasValue)
        {
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = @"SELECT COUNT(*) FROM categories 
                                        WHERE id = @categoryId AND skpd_id = @skpdId";
            checkCommand.Parameters.AddWithValue("@categoryId", request.CategoryId.Value);
            checkCommand.Parameters.AddWithValue("@skpdId", request.SkpdId);

            var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken));
            if (count == 0)
            {
                throw new InvalidOperationException(
                    $"Category ID {request.CategoryId.Value} tidak ditemukan atau bukan milik SKPD ID {request.SkpdId}");
            }
        }

        await using var command = connection.CreateCommand();

        var publishedAt = request.Status == "published" ? "UTC_TIMESTAMP()" : "NULL";

        command.CommandText = $@"INSERT INTO berita
                                (skpd_id, category_id, title, slug, excerpt, content, thumbnail_url, status, published_at, created_by)
                                VALUES
                                (@skpdId, @categoryId, @title, @slug, @excerpt, @content, @thumbnailUrl, @status, {publishedAt}, @createdBy);
                                SELECT LAST_INSERT_ID();";

        command.Parameters.AddWithValue("@skpdId", request.SkpdId);
        command.Parameters.AddWithValue("@categoryId", request.CategoryId.HasValue ? request.CategoryId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@title", request.Title);
        command.Parameters.AddWithValue("@slug", request.Slug);
        command.Parameters.AddWithValue("@excerpt", request.Excerpt);
        command.Parameters.AddWithValue("@content", request.Content);
        command.Parameters.AddWithValue("@thumbnailUrl", request.ThumbnailUrl);
        command.Parameters.AddWithValue("@status", request.Status);
        command.Parameters.AddWithValue("@createdBy", userId);

        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return (await GetByIdAsync(id, cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(long id, UpdateBeritaRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Check if berita exists and get current data
        var currentBerita = await GetByIdAsync(id, cancellationToken);
        if (currentBerita == null) return false;

        // Validate categoryId belongs to the same skpdId if categoryId is provided
        if (request.CategoryId.HasValue)
        {
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = @"SELECT COUNT(*) FROM categories 
                                        WHERE id = @categoryId AND skpd_id = @skpdId";
            checkCommand.Parameters.AddWithValue("@categoryId", request.CategoryId.Value);
            checkCommand.Parameters.AddWithValue("@skpdId", currentBerita.SkpdId);

            var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken));
            if (count == 0)
            {
                throw new InvalidOperationException(
                    $"Category ID {request.CategoryId.Value} tidak ditemukan atau bukan milik SKPD ID {currentBerita.SkpdId}");
            }
        }

        var shouldSetPublishedAt = currentBerita.Status != "published" && request.Status == "published";

        await using var command = connection.CreateCommand();

        var publishedAtClause = shouldSetPublishedAt ? "published_at = UTC_TIMESTAMP()," : "";

        command.CommandText = $@"UPDATE berita
                                SET category_id = @categoryId,
                                    title = @title,
                                    slug = @slug,
                                    excerpt = @excerpt,
                                    content = @content,
                                    thumbnail_url = @thumbnailUrl,
                                    status = @status,
                                    {publishedAtClause}
                                    updated_at = UTC_TIMESTAMP()
                                WHERE id = @id AND deleted_at IS NULL";

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@categoryId", request.CategoryId.HasValue ? request.CategoryId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@title", request.Title);
        command.Parameters.AddWithValue("@slug", request.Slug);
        command.Parameters.AddWithValue("@excerpt", request.Excerpt);
        command.Parameters.AddWithValue("@content", request.Content);
        command.Parameters.AddWithValue("@thumbnailUrl", request.ThumbnailUrl);
        command.Parameters.AddWithValue("@status", request.Status);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE berita
                                SET deleted_at = UTC_TIMESTAMP()
                                WHERE id = @id AND deleted_at IS NULL";
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> IncrementViewCountAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE berita
                                SET view_count = view_count + 1
                                WHERE id = @id AND deleted_at IS NULL";
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static Berita MapBerita(MySqlDataReader reader)
    {
        return new Berita
        {
            Id = reader.GetInt64("id"),
            SkpdId = reader.GetInt32("skpd_id"),
            SkpdNama = GetNullableString(reader, "skpd_nama"),
            CategoryId = GetNullableInt(reader, "category_id"),
            CategoryName = GetNullableString(reader, "category_name"),
            Title = reader.GetString("title"),
            Slug = reader.GetString("slug"),
            Excerpt = GetNullableString(reader, "excerpt"),
            Content = GetNullableString(reader, "content"),
            ThumbnailUrl = GetNullableString(reader, "thumbnail_url"),
            Status = reader.GetString("status"),
            PublishedAt = GetNullableDateTime(reader, "published_at"),
            ViewCount = reader.GetInt64("view_count"),
            CreatedBy = GetNullableLong(reader, "created_by"),
            CreatedByName = GetNullableString(reader, "created_by_name"),
            CreatedAt = reader.GetDateTime("created_at"),
            UpdatedAt = GetNullableDateTime(reader, "updated_at")
        };
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? GetNullableDateTime(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static long? GetNullableLong(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static int? GetNullableInt(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}