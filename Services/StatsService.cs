using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface IStatsService
{
    Task<DashboardStats> GetStatsAsync(int? skpdId, CancellationToken ct = default);
}

public sealed class StatsService(IMySqlConnectionFactory connectionFactory) : IStatsService
{
    public async Task<DashboardStats> GetStatsAsync(int? skpdId, CancellationToken ct = default)
    {
        var stats = new DashboardStats();

        // Setiap task membuka koneksinya sendiri — MySqlConnector tidak support concurrent use
        await Task.WhenAll(
            FillSkpdStatsAsync(skpdId, stats, ct),
            FillBeritaStatsAsync(skpdId, stats, ct),
            FillUserStatsAsync(skpdId, stats, ct),
            FillKategoriCountAsync(skpdId, stats, ct),
            FillBeritaPerBulanAsync(skpdId, stats, ct),
            FillRecentActivityAsync(skpdId, stats, ct)
        );

        return stats;
    }

    // ── SKPD ─────────────────────────────────────────────────────────────────
    private async Task FillSkpdStatsAsync(int? skpdId, DashboardStats stats, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        if (skpdId.HasValue)
        {
            cmd.CommandText = @"
                SELECT
                    1                                           AS total,
                    CAST(is_active AS UNSIGNED)                 AS active,
                    CAST(1 - is_active AS UNSIGNED)             AS inactive
                FROM skpd WHERE id = @skpdId";
            cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);
        }
        else
        {
            cmd.CommandText = @"
                SELECT
                    COUNT(*)                                                    AS total,
                    SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END)             AS active,
                    SUM(CASE WHEN is_active = 0 THEN 1 ELSE 0 END)             AS inactive
                FROM skpd";
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            stats.Skpd = new SkpdStats
            {
                Total = reader.GetInt32("total"),
                Active = reader.GetInt32("active"),
                Inactive = reader.GetInt32("inactive"),
            };
        }
    }

    // ── Berita ────────────────────────────────────────────────────────────────
    private async Task FillBeritaStatsAsync(int? skpdId, DashboardStats stats, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var where = skpdId.HasValue ? "WHERE skpd_id = @skpdId" : string.Empty;
        if (skpdId.HasValue) cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);

        cmd.CommandText = $@"
            SELECT
                COUNT(*)                                                        AS total,
                SUM(CASE WHEN status = 'published' THEN 1 ELSE 0 END)          AS published,
                SUM(CASE WHEN status = 'draft'     THEN 1 ELSE 0 END)          AS draft,
                SUM(CASE WHEN status = 'review'    THEN 1 ELSE 0 END)          AS review,
                COALESCE(SUM(view_count), 0)                                    AS total_views
            FROM berita {where}";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            stats.Berita = new BeritaStats
            {
                Total = reader.GetInt32("total"),
                Published = reader.GetInt32("published"),
                Draft = reader.GetInt32("draft"),
                Review = reader.GetInt32("review"),
                TotalViews = reader.GetInt64("total_views"),
            };
        }
    }

    // ── Users ─────────────────────────────────────────────────────────────────
    private async Task FillUserStatsAsync(int? skpdId, DashboardStats stats, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var where = skpdId.HasValue ? "WHERE skpd_id = @skpdId" : string.Empty;
        if (skpdId.HasValue) cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);

        cmd.CommandText = $@"
            SELECT
                COUNT(*)                                                        AS total,
                SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END)                 AS active,
                SUM(CASE WHEN is_active = 0 THEN 1 ELSE 0 END)                 AS inactive
            FROM users {where}";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            stats.Users = new UserStats
            {
                Total = reader.GetInt32("total"),
                Active = reader.GetInt32("active"),
                Inactive = reader.GetInt32("inactive"),
            };
        }
    }

    // ── Kategori ──────────────────────────────────────────────────────────────
    private async Task FillKategoriCountAsync(int? skpdId, DashboardStats stats, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var where = skpdId.HasValue ? "WHERE skpd_id = @skpdId" : string.Empty;
        if (skpdId.HasValue) cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);

        cmd.CommandText = $"SELECT COUNT(*) FROM categories {where}";
        stats.TotalKategori = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    // ── Berita per bulan (6 bulan terakhir) ──────────────────────────────────
    private async Task FillBeritaPerBulanAsync(int? skpdId, DashboardStats stats, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var andWhere = skpdId.HasValue ? "AND skpd_id = @skpdId" : string.Empty;
        if (skpdId.HasValue) cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);

        cmd.CommandText = $@"
            SELECT
                DATE_FORMAT(created_at, '%Y-%m')                                AS month,
                COUNT(*)                                                        AS total,
                SUM(CASE WHEN status = 'published' THEN 1 ELSE 0 END)          AS published
            FROM berita
            WHERE created_at >= DATE_SUB(UTC_TIMESTAMP(), INTERVAL 6 MONTH)
            {andWhere}
            GROUP BY DATE_FORMAT(created_at, '%Y-%m')
            ORDER BY month ASC";

        var list = new List<MonthlyBeritaCount>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new MonthlyBeritaCount
            {
                Month = reader.GetString("month"),
                Total = reader.GetInt32("total"),
                Published = reader.GetInt32("published"),
            });
        }
        stats.BeritaPerBulan = list;
    }

    // ── Recent audit activity (5 terakhir) ───────────────────────────────────
    private async Task FillRecentActivityAsync(int? skpdId, DashboardStats stats, CancellationToken ct)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        var where = skpdId.HasValue ? "WHERE skpd_id = @skpdId" : string.Empty;
        if (skpdId.HasValue) cmd.Parameters.AddWithValue("@skpdId", skpdId.Value);

        cmd.CommandText = $@"
            SELECT id, action, event_type, identity, entity_type, entity_id, status, created_at
            FROM audit_logs {where}
            ORDER BY created_at DESC
            LIMIT 5";

        var list = new List<RecentAuditActivity>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RecentAuditActivity
            {
                Id = reader.GetInt64("id"),
                Action = reader.GetString("action"),
                EventType = reader.GetString("event_type"),
                Identity = reader.GetString("identity"),
                EntityType = reader.IsDBNull(reader.GetOrdinal("entity_type")) ? null : reader.GetString("entity_type"),
                EntityId = reader.IsDBNull(reader.GetOrdinal("entity_id")) ? null : reader.GetInt64("entity_id"),
                Status = reader.GetString("status"),
                CreatedAt = reader.GetDateTime("created_at"),
            });
        }
        stats.RecentActivity = list;
    }
}
