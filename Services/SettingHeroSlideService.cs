using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISettingHeroSlideService
{
    Task<List<SettingHeroSlide>> GetAllAsync(CancellationToken ct = default);
    Task<SettingHeroSlide?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SettingHeroSlide> CreateAsync(UpsertSettingHeroSlideRequest request, CancellationToken ct = default);
    Task<SettingHeroSlide?> UpdateAsync(int id, UpsertSettingHeroSlideRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task ReorderAsync(int[] orderedIds, CancellationToken ct = default);
}

public sealed class SettingHeroSlideService(IMySqlConnectionFactory connectionFactory) : ISettingHeroSlideService
{
    public async Task<List<SettingHeroSlide>> GetAllAsync(CancellationToken ct = default)
    {
        var items = new List<SettingHeroSlide>();
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, image_url, title, subtitle, button_text, button_url,
                   button_target, text_align, overlay_opacity, sort_order,
                   is_active, created_at, updated_at
            FROM setting_hero_slides
            ORDER BY sort_order ASC, id ASC";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(ReadSlide(reader));

        return items;
    }

    public async Task<SettingHeroSlide?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, image_url, title, subtitle, button_text, button_url,
                   button_target, text_align, overlay_opacity, sort_order,
                   is_active, created_at, updated_at
            FROM setting_hero_slides
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSlide(reader) : null;
    }

    public async Task<SettingHeroSlide> CreateAsync(UpsertSettingHeroSlideRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO setting_hero_slides
                (image_url, title, subtitle, button_text, button_url,
                 button_target, text_align, overlay_opacity, sort_order, is_active, created_at)
            VALUES
                (@imageUrl, @title, @subtitle, @buttonText, @buttonUrl,
                 @buttonTarget, @textAlign, @overlayOpacity, @sortOrder, @isActive, NOW());
            SELECT LAST_INSERT_ID();";

        AddParams(cmd, request);
        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

        return await GetByIdAsync(newId, ct)
            ?? throw new InvalidOperationException("Gagal mengambil slide setelah insert.");
    }

    public async Task<SettingHeroSlide?> UpdateAsync(int id, UpsertSettingHeroSlideRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE setting_hero_slides
            SET image_url       = @imageUrl,
                title           = @title,
                subtitle        = @subtitle,
                button_text     = @buttonText,
                button_url      = @buttonUrl,
                button_target   = @buttonTarget,
                text_align      = @textAlign,
                overlay_opacity = @overlayOpacity,
                sort_order      = @sortOrder,
                is_active       = @isActive,
                updated_at      = NOW()
            WHERE id = @id";

        cmd.Parameters.AddWithValue("@id", id);
        AddParams(cmd, request);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0 ? await GetByIdAsync(id, ct) : null;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM setting_hero_slides WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task ReorderAsync(int[] orderedIds, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        for (int i = 0; i < orderedIds.Length; i++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE setting_hero_slides SET sort_order = @order, updated_at = NOW() WHERE id = @id";
            cmd.Parameters.AddWithValue("@order", i);
            cmd.Parameters.AddWithValue("@id", orderedIds[i]);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static SettingHeroSlide ReadSlide(System.Data.Common.DbDataReader reader)
    {
        return new SettingHeroSlide
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            ImageUrl = reader.GetString(reader.GetOrdinal("image_url")),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
            Subtitle = reader.IsDBNull(reader.GetOrdinal("subtitle")) ? null : reader.GetString(reader.GetOrdinal("subtitle")),
            ButtonText = reader.IsDBNull(reader.GetOrdinal("button_text")) ? null : reader.GetString(reader.GetOrdinal("button_text")),
            ButtonUrl = reader.IsDBNull(reader.GetOrdinal("button_url")) ? null : reader.GetString(reader.GetOrdinal("button_url")),
            ButtonTarget = reader.IsDBNull(reader.GetOrdinal("button_target")) ? "_self" : reader.GetString(reader.GetOrdinal("button_target")),
            TextAlign = reader.IsDBNull(reader.GetOrdinal("text_align")) ? "middle-center" : reader.GetString(reader.GetOrdinal("text_align")),
            OverlayOpacity = reader.GetDecimal(reader.GetOrdinal("overlay_opacity")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? null : reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
        };
    }

    private static void AddParams(MySqlConnector.MySqlCommand cmd, UpsertSettingHeroSlideRequest r)
    {
        cmd.Parameters.AddWithValue("@imageUrl", r.ImageUrl);
        cmd.Parameters.AddWithValue("@title", (object?)r.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@subtitle", (object?)r.Subtitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buttonText", (object?)r.ButtonText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buttonUrl", (object?)r.ButtonUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buttonTarget", r.ButtonTarget);
        cmd.Parameters.AddWithValue("@textAlign", r.TextAlign);
        cmd.Parameters.AddWithValue("@overlayOpacity", r.OverlayOpacity);
        cmd.Parameters.AddWithValue("@sortOrder", r.SortOrder);
        cmd.Parameters.AddWithValue("@isActive", r.IsActive);
    }
}
