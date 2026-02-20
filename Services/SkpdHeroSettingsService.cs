using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISkpdHeroSettingsService
{
    Task<SkpdHeroSettings?> GetBySkpdIdAsync(int skpdId, CancellationToken ct = default);
    Task<SkpdHeroSettings> UpsertAsync(int skpdId, UpsertSkpdHeroSettingsRequest request, CancellationToken ct = default);
}

public sealed class SkpdHeroSettingsService(IMySqlConnectionFactory connectionFactory) : ISkpdHeroSettingsService
{
    public async Task<SkpdHeroSettings?> GetBySkpdIdAsync(int skpdId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, skpd_id, hero_type, title, subtitle, background_image, overlay_opacity
            FROM skpd_hero_settings
            WHERE skpd_id = @skpdId
            LIMIT 1";
        cmd.Parameters.AddWithValue("@skpdId", skpdId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new SkpdHeroSettings
        {
            Id = reader.GetInt32("id"),
            SkpdId = reader.GetInt32("skpd_id"),
            HeroType = reader.GetString("hero_type"),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString("title"),
            Subtitle = reader.IsDBNull(reader.GetOrdinal("subtitle")) ? null : reader.GetString("subtitle"),
            BackgroundImage = reader.IsDBNull(reader.GetOrdinal("background_image")) ? null : reader.GetString("background_image"),
            OverlayOpacity = reader.GetDecimal("overlay_opacity")
        };
    }

    public async Task<SkpdHeroSettings> UpsertAsync(int skpdId, UpsertSkpdHeroSettingsRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO skpd_hero_settings
                (skpd_id, hero_type, title, subtitle, background_image, overlay_opacity)
            VALUES
                (@skpdId, @heroType, @title, @subtitle, @backgroundImage, @overlayOpacity)
            ON DUPLICATE KEY UPDATE
                hero_type = VALUES(hero_type),
                title = VALUES(title),
                subtitle = VALUES(subtitle),
                background_image = VALUES(background_image),
                overlay_opacity = VALUES(overlay_opacity)";

        cmd.Parameters.AddWithValue("@skpdId", skpdId);
        cmd.Parameters.AddWithValue("@heroType", request.HeroType);
        cmd.Parameters.AddWithValue("@title", (object?)request.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@subtitle", (object?)request.Subtitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@backgroundImage", (object?)request.BackgroundImage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@overlayOpacity", request.OverlayOpacity);

        await cmd.ExecuteNonQueryAsync(ct);

        var item = await GetBySkpdIdAsync(skpdId, ct);
        return item ?? throw new InvalidOperationException("Gagal mengambil data hero setting setelah upsert.");
    }
}
