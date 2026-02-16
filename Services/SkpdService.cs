using MySqlConnector;
using skpd_multi_tenant.Models;

namespace skpd_multi_tenant.Services;

public interface ISkpdService
{
    Task<IReadOnlyList<Skpd>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Skpd?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Skpd> CreateAsync(CreateSkpdRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, UpdateSkpdRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class SkpdService(IMySqlConnectionFactory connectionFactory) : ISkpdService
{
    public async Task<IReadOnlyList<Skpd>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<Skpd>();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, kode, nama, slug, domain, logo_url, primary_color, secondary_color,
                                       theme_type, layout_type, is_active, created_at, updated_at
                                FROM skpd
                                WHERE deleted_at IS NULL
                                ORDER BY id DESC";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapSkpd(reader));
        }

        return items;
    }

    public async Task<Skpd?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, kode, nama, slug, domain, logo_url, primary_color, secondary_color,
                                       theme_type, layout_type, is_active, created_at, updated_at
                                FROM skpd
                                WHERE id = @id AND deleted_at IS NULL
                                LIMIT 1";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapSkpd(reader);
    }

    public async Task<Skpd> CreateAsync(CreateSkpdRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO skpd
                                (kode, nama, slug, domain, logo_url, primary_color, secondary_color, theme_type, layout_type, is_active)
                                VALUES
                                (@kode, @nama, @slug, @domain, @logoUrl, @primaryColor, @secondaryColor, @themeType, @layoutType, 1);
                                SELECT LAST_INSERT_ID();";
        command.Parameters.AddWithValue("@kode", request.Kode);
        command.Parameters.AddWithValue("@nama", request.Nama);
        command.Parameters.AddWithValue("@slug", request.Slug);
        command.Parameters.AddWithValue("@domain", request.Domain);
        command.Parameters.AddWithValue("@logoUrl", request.LogoUrl);
        command.Parameters.AddWithValue("@primaryColor", request.PrimaryColor);
        command.Parameters.AddWithValue("@secondaryColor", request.SecondaryColor);
        command.Parameters.AddWithValue("@themeType", request.ThemeType);
        command.Parameters.AddWithValue("@layoutType", request.LayoutType);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return (await GetByIdAsync(id, cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(int id, UpdateSkpdRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE skpd
                                SET nama = @nama,
                                    domain = @domain,
                                    logo_url = @logoUrl,
                                    primary_color = @primaryColor,
                                    secondary_color = @secondaryColor,
                                    theme_type = @themeType,
                                    layout_type = @layoutType,
                                    is_active = @isActive,
                                    updated_at = UTC_TIMESTAMP()
                                WHERE id = @id AND deleted_at IS NULL";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@nama", request.Nama);
        command.Parameters.AddWithValue("@domain", request.Domain);
        command.Parameters.AddWithValue("@logoUrl", request.LogoUrl);
        command.Parameters.AddWithValue("@primaryColor", request.PrimaryColor);
        command.Parameters.AddWithValue("@secondaryColor", request.SecondaryColor);
        command.Parameters.AddWithValue("@themeType", request.ThemeType);
        command.Parameters.AddWithValue("@layoutType", request.LayoutType);
        command.Parameters.AddWithValue("@isActive", request.IsActive);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE skpd
                                SET deleted_at = UTC_TIMESTAMP(), is_active = 0
                                WHERE id = @id AND deleted_at IS NULL";
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static Skpd MapSkpd(MySqlDataReader reader)
    {
        return new Skpd
        {
            Id = reader.GetInt32("id"),
            Kode = reader.GetString("kode"),
            Nama = reader.GetString("nama"),
            Slug = reader.GetString("slug"),
            Domain = GetNullableString(reader, "domain"),
            LogoUrl = GetNullableString(reader, "logo_url"),
            PrimaryColor = GetNullableString(reader, "primary_color"),
            SecondaryColor = GetNullableString(reader, "secondary_color"),
            ThemeType = GetNullableString(reader, "theme_type"),
            LayoutType = GetNullableString(reader, "layout_type"),
            IsActive = reader.GetBoolean("is_active"),
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
}
