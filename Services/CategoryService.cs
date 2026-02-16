using MySqlConnector;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CategoryQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetBySkpdIdAsync(int skpdId, CancellationToken cancellationToken = default);
    Task<Category> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class CategoryService(IMySqlConnectionFactory connectionFactory) : ICategoryService
{
    public async Task<IReadOnlyList<Category>> GetAllAsync(CategoryQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var items = new List<Category>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var whereConditions = new List<string>();

        if (queryParams.SkpdId.HasValue)
        {
            whereConditions.Add("c.skpd_id = @skpdId");
            command.Parameters.AddWithValue("@skpdId", queryParams.SkpdId.Value);
        }

        var whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";
        var offset = (queryParams.Page - 1) * queryParams.PageSize;

        command.CommandText = $@"SELECT c.id, c.skpd_id, s.nama as skpd_nama, c.name, c.slug, c.created_at
                                FROM categories c
                                LEFT JOIN skpd s ON c.skpd_id = s.id
                                {whereClause}
                                ORDER BY c.created_at DESC
                                LIMIT @limit OFFSET @offset";

        command.Parameters.AddWithValue("@limit", queryParams.PageSize);
        command.Parameters.AddWithValue("@offset", offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapCategory(reader));
        }

        return items;
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT c.id, c.skpd_id, s.nama as skpd_nama, c.name, c.slug, c.created_at
                                FROM categories c
                                LEFT JOIN skpd s ON c.skpd_id = s.id
                                WHERE c.id = @id
                                LIMIT 1";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapCategory(reader);
    }

    public async Task<IReadOnlyList<Category>> GetBySkpdIdAsync(int skpdId, CancellationToken cancellationToken = default)
    {
        var items = new List<Category>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = @"SELECT c.id, c.skpd_id, s.nama as skpd_nama, c.name, c.slug, c.created_at
                                FROM categories c
                                LEFT JOIN skpd s ON c.skpd_id = s.id
                                WHERE c.skpd_id = @skpdId
                                ORDER BY c.name ASC";

        command.Parameters.AddWithValue("@skpdId", skpdId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapCategory(reader));
        }

        return items;
    }

    public async Task<Category> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = @"INSERT INTO categories
                                (skpd_id, name, slug)
                                VALUES
                                (@skpdId, @name, @slug);
                                SELECT LAST_INSERT_ID();";

        command.Parameters.AddWithValue("@skpdId", request.SkpdId);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@slug", request.Slug);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return (await GetByIdAsync(id, cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = @"UPDATE categories
                                SET name = @name,
                                    slug = @slug
                                WHERE id = @id";

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@slug", request.Slug);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // Hard delete - tidak ada soft delete di tabel categories
        command.CommandText = @"DELETE FROM categories WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static Category MapCategory(MySqlDataReader reader)
    {
        return new Category
        {
            Id = reader.GetInt32("id"),
            SkpdId = reader.GetInt32("skpd_id"),
            SkpdNama = GetNullableString(reader, "skpd_nama"),
            Name = GetNullableString(reader, "name"),
            Slug = GetNullableString(reader, "slug"),
            CreatedAt = reader.GetDateTime("created_at")
        };
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}