using System.Data;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISkpdSectionsService
{
    Task<IReadOnlyList<SkpdSection>> GetAllAsync(int skpdId, CancellationToken ct = default);
    Task<SkpdSection?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SkpdSection> CreateAsync(int skpdId, CreateSkpdSectionRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateSkpdSectionRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> ReorderAsync(int skpdId, IReadOnlyList<int> orderedSectionIds, CancellationToken ct = default);
}

public sealed class SkpdSectionsService(IMySqlConnectionFactory connectionFactory) : ISkpdSectionsService
{
    public async Task<IReadOnlyList<SkpdSection>> GetAllAsync(int skpdId, CancellationToken ct = default)
    {
        var items = new List<SkpdSection>();
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, skpd_id, section_code, is_enabled, sort_order, custom_title
            FROM skpd_sections
            WHERE skpd_id = @skpdId
            ORDER BY sort_order, id";
        cmd.Parameters.AddWithValue("@skpdId", skpdId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapSection(reader));
        }

        return items;
    }

    public async Task<SkpdSection?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, skpd_id, section_code, is_enabled, sort_order, custom_title
            FROM skpd_sections
            WHERE id = @id
            LIMIT 1";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapSection(reader);
    }

    public async Task<SkpdSection> CreateAsync(int skpdId, CreateSkpdSectionRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO skpd_sections (skpd_id, section_code, is_enabled, sort_order, custom_title)
            VALUES (@skpdId, @sectionCode, @isEnabled, @sortOrder, @customTitle);
            SELECT LAST_INSERT_ID();";
        cmd.Parameters.AddWithValue("@skpdId", skpdId);
        cmd.Parameters.AddWithValue("@sectionCode", request.SectionCode.Trim());
        cmd.Parameters.AddWithValue("@isEnabled", request.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@sortOrder", request.SortOrder);
        cmd.Parameters.AddWithValue("@customTitle", (object?)request.CustomTitle?.Trim() ?? DBNull.Value);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        var item = await GetByIdAsync(insertedId, ct);
        return item ?? throw new InvalidOperationException("Gagal membaca section setelah create.");
    }

    public async Task<bool> UpdateAsync(int id, UpdateSkpdSectionRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE skpd_sections
            SET section_code = @sectionCode,
                is_enabled = @isEnabled,
                sort_order = @sortOrder,
                custom_title = @customTitle
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@sectionCode", request.SectionCode.Trim());
        cmd.Parameters.AddWithValue("@isEnabled", request.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@sortOrder", request.SortOrder);
        cmd.Parameters.AddWithValue("@customTitle", (object?)request.CustomTitle?.Trim() ?? DBNull.Value);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM skpd_sections WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    public async Task<bool> ReorderAsync(int skpdId, IReadOnlyList<int> orderedSectionIds, CancellationToken ct = default)
    {
        if (orderedSectionIds.Count == 0) return true;

        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Validate all IDs belong to requested SKPD.
            await using (var validateCmd = conn.CreateCommand())
            {
                validateCmd.Transaction = tx;
                validateCmd.CommandText = $@"
                    SELECT COUNT(*)
                    FROM skpd_sections
                    WHERE skpd_id = @skpdId
                      AND id IN ({string.Join(",", orderedSectionIds.Select((_, i) => $"@id{i}"))})";
                validateCmd.Parameters.AddWithValue("@skpdId", skpdId);
                for (var i = 0; i < orderedSectionIds.Count; i++)
                {
                    validateCmd.Parameters.AddWithValue($"@id{i}", orderedSectionIds[i]);
                }

                var count = Convert.ToInt32(await validateCmd.ExecuteScalarAsync(ct));
                if (count != orderedSectionIds.Count)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }
            }

            for (var i = 0; i < orderedSectionIds.Count; i++)
            {
                await using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
                    UPDATE skpd_sections
                    SET sort_order = @sortOrder
                    WHERE id = @id AND skpd_id = @skpdId";
                updateCmd.Parameters.AddWithValue("@sortOrder", i + 1);
                updateCmd.Parameters.AddWithValue("@id", orderedSectionIds[i]);
                updateCmd.Parameters.AddWithValue("@skpdId", skpdId);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static SkpdSection MapSection(System.Data.Common.DbDataReader reader)
    {
        return new SkpdSection
        {
            Id = reader.GetInt32("id"),
            SkpdId = reader.GetInt32("skpd_id"),
            SectionCode = reader.IsDBNull(reader.GetOrdinal("section_code")) ? string.Empty : reader.GetString("section_code"),
            IsEnabled = reader.GetBoolean("is_enabled"),
            SortOrder = reader.GetInt32("sort_order"),
            CustomTitle = reader.IsDBNull(reader.GetOrdinal("custom_title")) ? null : reader.GetString("custom_title"),
        };
    }
}
