using System.Data;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISkpdTemplatesService
{
    Task<SkpdTemplateSetting?> GetBySkpdIdAsync(int skpdId, CancellationToken ct = default);
    Task<SkpdTemplateSetting> UpsertAsync(int skpdId, UpsertSkpdTemplateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateOption>> GetTemplateOptionsAsync(CancellationToken ct = default);
    Task<bool> IsTemplateExistsAsync(int templateId, CancellationToken ct = default);
}

public sealed class SkpdTemplatesService(IMySqlConnectionFactory connectionFactory) : ISkpdTemplatesService
{
    public async Task<SkpdTemplateSetting?> GetBySkpdIdAsync(int skpdId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, skpd_id, template_id, primary_color, secondary_color, font_family,
                   header_style, footer_style, hero_layout, custom_css, created_at, updated_at
            FROM skpd_templates
            WHERE skpd_id = @skpdId
            LIMIT 1";
        cmd.Parameters.AddWithValue("@skpdId", skpdId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapSetting(reader);
    }

    public async Task<SkpdTemplateSetting> UpsertAsync(int skpdId, UpsertSkpdTemplateRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO skpd_templates
                (skpd_id, template_id, primary_color, secondary_color, font_family, header_style, footer_style, hero_layout, custom_css, updated_at)
            VALUES
                (@skpdId, @templateId, @primaryColor, @secondaryColor, @fontFamily, @headerStyle, @footerStyle, @heroLayout, @customCss, UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                template_id = VALUES(template_id),
                primary_color = VALUES(primary_color),
                secondary_color = VALUES(secondary_color),
                font_family = VALUES(font_family),
                header_style = VALUES(header_style),
                footer_style = VALUES(footer_style),
                hero_layout = VALUES(hero_layout),
                custom_css = VALUES(custom_css),
                updated_at = UTC_TIMESTAMP()";

        cmd.Parameters.AddWithValue("@skpdId", skpdId);
        cmd.Parameters.AddWithValue("@templateId", request.TemplateId);
        cmd.Parameters.AddWithValue("@primaryColor", (object?)request.PrimaryColor?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@secondaryColor", (object?)request.SecondaryColor?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fontFamily", (object?)request.FontFamily?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@headerStyle", (object?)request.HeaderStyle?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@footerStyle", (object?)request.FooterStyle?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@heroLayout", (object?)request.HeroLayout?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@customCss", (object?)request.CustomCss ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);

        var item = await GetBySkpdIdAsync(skpdId, ct);
        return item ?? throw new InvalidOperationException("Gagal membaca template setting setelah upsert.");
    }

    public async Task<IReadOnlyList<TemplateOption>> GetTemplateOptionsAsync(CancellationToken ct = default)
    {
        var items = new List<TemplateOption>();
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, code, description, preview_image, is_active
            FROM templates
            ORDER BY is_active DESC, id ASC";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new TemplateOption
            {
                Id = reader.GetInt32("id"),
                Name = reader.GetString("name"),
                Code = reader.GetString("code"),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString("description"),
                PreviewImage = reader.IsDBNull(reader.GetOrdinal("preview_image")) ? null : reader.GetString("preview_image"),
                IsActive = reader.GetBoolean("is_active")
            });
        }

        return items;
    }

    public async Task<bool> IsTemplateExistsAsync(int templateId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM templates WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", templateId);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return count > 0;
    }

    private static SkpdTemplateSetting MapSetting(System.Data.Common.DbDataReader reader)
    {
        return new SkpdTemplateSetting
        {
            Id = reader.GetInt32("id"),
            SkpdId = reader.GetInt32("skpd_id"),
            TemplateId = reader.GetInt32("template_id"),
            PrimaryColor = reader.IsDBNull(reader.GetOrdinal("primary_color")) ? null : reader.GetString("primary_color"),
            SecondaryColor = reader.IsDBNull(reader.GetOrdinal("secondary_color")) ? null : reader.GetString("secondary_color"),
            FontFamily = reader.IsDBNull(reader.GetOrdinal("font_family")) ? null : reader.GetString("font_family"),
            HeaderStyle = reader.IsDBNull(reader.GetOrdinal("header_style")) ? null : reader.GetString("header_style"),
            FooterStyle = reader.IsDBNull(reader.GetOrdinal("footer_style")) ? null : reader.GetString("footer_style"),
            HeroLayout = reader.IsDBNull(reader.GetOrdinal("hero_layout")) ? null : reader.GetString("hero_layout"),
            CustomCss = reader.IsDBNull(reader.GetOrdinal("custom_css")) ? null : reader.GetString("custom_css"),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? null : reader.GetDateTime("created_at"),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at"),
        };
    }
}
