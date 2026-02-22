using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISkpdHeroSettingsService
{
    Task<SkpdHeroSettings?> GetBySkpdIdAsync(int skpdId, CancellationToken ct = default);
    Task<SkpdHeroSettings> UpsertAsync(int skpdId, UpsertSkpdHeroSettingsRequest request, CancellationToken ct = default);

    // Slides
    Task<List<SkpdHeroSlide>> GetSlidesByHeroIdAsync(int heroSettingId, CancellationToken ct = default);
    Task<SkpdHeroSlide?> GetSlideByIdAsync(int slideId, CancellationToken ct = default);
    Task<SkpdHeroSlide> CreateSlideAsync(UpsertSkpdHeroSlideRequest request, CancellationToken ct = default);
    Task<SkpdHeroSlide?> UpdateSlideAsync(int slideId, UpsertSkpdHeroSlideRequest request, CancellationToken ct = default);
    Task<bool> DeleteSlideAsync(int slideId, CancellationToken ct = default);
    Task ReorderSlidesAsync(int heroSettingId, int[] orderedIds, CancellationToken ct = default);
}

public sealed class SkpdHeroSettingsService(IMySqlConnectionFactory connectionFactory) : ISkpdHeroSettingsService
{
    // ─── Hero Settings ────────────────────────────────────────────────

    public async Task<SkpdHeroSettings?> GetBySkpdIdAsync(int skpdId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, skpd_id, hero_type, overlay_opacity, height,
                   title, subtitle, video_url, is_active, created_at, updated_at
            FROM skpd_hero_settings
            WHERE skpd_id = @skpdId
            LIMIT 1";
        cmd.Parameters.AddWithValue("@skpdId", skpdId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var hero = ReadHeroSettings(reader);
        await reader.CloseAsync();

        // Load slides if slider type
        if (hero.HeroType == "slider")
            hero.Slides = await GetSlidesByHeroIdAsync(hero.Id, ct);

        return hero;
    }

    public async Task<SkpdHeroSettings> UpsertAsync(int skpdId, UpsertSkpdHeroSettingsRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO skpd_hero_settings
                (skpd_id, hero_type, overlay_opacity, height, title, subtitle, video_url, is_active, created_at)
            VALUES
                (@skpdId, @heroType, @overlayOpacity, @height, @title, @subtitle, @videoUrl, @isActive, NOW())
            ON DUPLICATE KEY UPDATE
                hero_type = VALUES(hero_type),
                overlay_opacity = VALUES(overlay_opacity),
                height = VALUES(height),
                title = VALUES(title),
                subtitle = VALUES(subtitle),
                video_url = VALUES(video_url),
                is_active = VALUES(is_active),
                updated_at = NOW()";

        cmd.Parameters.AddWithValue("@skpdId", skpdId);
        cmd.Parameters.AddWithValue("@heroType", request.HeroType);
        cmd.Parameters.AddWithValue("@overlayOpacity", request.OverlayOpacity);
        cmd.Parameters.AddWithValue("@height", request.Height);
        cmd.Parameters.AddWithValue("@title", (object?)request.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@subtitle", (object?)request.Subtitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@videoUrl", (object?)request.VideoUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isActive", request.IsActive);

        await cmd.ExecuteNonQueryAsync(ct);

        var item = await GetBySkpdIdAsync(skpdId, ct);
        return item ?? throw new InvalidOperationException("Gagal mengambil data hero setting setelah upsert.");
    }

    // ─── Slides ───────────────────────────────────────────────────────

    public async Task<List<SkpdHeroSlide>> GetSlidesByHeroIdAsync(int heroSettingId, CancellationToken ct = default)
    {
        var items = new List<SkpdHeroSlide>();
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, hero_setting_id, image_url, title, subtitle,
                   button_text, button_url, button_target, text_align,
                   sort_order, is_active, created_at, updated_at
            FROM skpd_hero_slides
            WHERE hero_setting_id = @heroSettingId
            ORDER BY sort_order ASC, id ASC";
        cmd.Parameters.AddWithValue("@heroSettingId", heroSettingId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(ReadSlide(reader));

        return items;
    }

    public async Task<SkpdHeroSlide?> GetSlideByIdAsync(int slideId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, hero_setting_id, image_url, title, subtitle,
                   button_text, button_url, button_target, text_align,
                   sort_order, is_active, created_at, updated_at
            FROM skpd_hero_slides
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", slideId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSlide(reader) : null;
    }

    public async Task<SkpdHeroSlide> CreateSlideAsync(UpsertSkpdHeroSlideRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO skpd_hero_slides
                (hero_setting_id, image_url, title, subtitle, button_text, button_url,
                 button_target, text_align, sort_order, is_active, created_at)
            VALUES
                (@heroSettingId, @imageUrl, @title, @subtitle, @buttonText, @buttonUrl,
                 @buttonTarget, @textAlign, @sortOrder, @isActive, NOW());
            SELECT LAST_INSERT_ID();";

        AddSlideParams(cmd, request);
        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

        return await GetSlideByIdAsync(newId, ct)
            ?? throw new InvalidOperationException("Gagal mengambil slide setelah insert.");
    }

    public async Task<SkpdHeroSlide?> UpdateSlideAsync(int slideId, UpsertSkpdHeroSlideRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE skpd_hero_slides
            SET image_url = @imageUrl,
                title = @title,
                subtitle = @subtitle,
                button_text = @buttonText,
                button_url = @buttonUrl,
                button_target = @buttonTarget,
                text_align = @textAlign,
                sort_order = @sortOrder,
                is_active = @isActive,
                updated_at = NOW()
            WHERE id = @id";

        cmd.Parameters.AddWithValue("@id", slideId);
        AddSlideParams(cmd, request);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0 ? await GetSlideByIdAsync(slideId, ct) : null;
    }

    public async Task<bool> DeleteSlideAsync(int slideId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM skpd_hero_slides WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", slideId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task ReorderSlidesAsync(int heroSettingId, int[] orderedIds, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        for (int i = 0; i < orderedIds.Length; i++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE skpd_hero_slides SET sort_order = @order, updated_at = NOW() WHERE id = @id AND hero_setting_id = @heroId";
            cmd.Parameters.AddWithValue("@order", i);
            cmd.Parameters.AddWithValue("@id", orderedIds[i]);
            cmd.Parameters.AddWithValue("@heroId", heroSettingId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private static SkpdHeroSettings ReadHeroSettings(System.Data.Common.DbDataReader reader)
    {
        return new SkpdHeroSettings
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            SkpdId = reader.GetInt32(reader.GetOrdinal("skpd_id")),
            HeroType = reader.GetString(reader.GetOrdinal("hero_type")),
            OverlayOpacity = reader.GetDecimal(reader.GetOrdinal("overlay_opacity")),
            Height = reader.IsDBNull(reader.GetOrdinal("height")) ? "500px" : reader.GetString(reader.GetOrdinal("height")),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
            Subtitle = reader.IsDBNull(reader.GetOrdinal("subtitle")) ? null : reader.GetString(reader.GetOrdinal("subtitle")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("video_url")) ? null : reader.GetString(reader.GetOrdinal("video_url")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? null : reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
        };
    }

    private static SkpdHeroSlide ReadSlide(System.Data.Common.DbDataReader reader)
    {
        return new SkpdHeroSlide
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            HeroSettingId = reader.GetInt32(reader.GetOrdinal("hero_setting_id")),
            ImageUrl = reader.GetString(reader.GetOrdinal("image_url")),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
            Subtitle = reader.IsDBNull(reader.GetOrdinal("subtitle")) ? null : reader.GetString(reader.GetOrdinal("subtitle")),
            ButtonText = reader.IsDBNull(reader.GetOrdinal("button_text")) ? null : reader.GetString(reader.GetOrdinal("button_text")),
            ButtonUrl = reader.IsDBNull(reader.GetOrdinal("button_url")) ? null : reader.GetString(reader.GetOrdinal("button_url")),
            ButtonTarget = reader.IsDBNull(reader.GetOrdinal("button_target")) ? "_self" : reader.GetString(reader.GetOrdinal("button_target")),
            TextAlign = reader.IsDBNull(reader.GetOrdinal("text_align")) ? "center" : reader.GetString(reader.GetOrdinal("text_align")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? null : reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
        };
    }

    private static void AddSlideParams(MySqlConnector.MySqlCommand cmd, UpsertSkpdHeroSlideRequest r)
    {
        cmd.Parameters.AddWithValue("@heroSettingId", r.HeroSettingId);
        cmd.Parameters.AddWithValue("@imageUrl", r.ImageUrl);
        cmd.Parameters.AddWithValue("@title", (object?)r.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@subtitle", (object?)r.Subtitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buttonText", (object?)r.ButtonText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buttonUrl", (object?)r.ButtonUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buttonTarget", r.ButtonTarget);
        cmd.Parameters.AddWithValue("@textAlign", r.TextAlign);
        cmd.Parameters.AddWithValue("@sortOrder", r.SortOrder);
        cmd.Parameters.AddWithValue("@isActive", r.IsActive);
    }
}
