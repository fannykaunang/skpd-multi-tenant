using System.Data;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ITemplateSectionService
{
    Task<IReadOnlyList<TemplateSection>> GetAllByTemplateIdAsync(int templateId, CancellationToken ct = default);
    Task<TemplateSection?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TemplateSection> CreateAsync(CreateTemplateSectionRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateTemplateSectionRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task ReorderAsync(int templateId, int[] orderedIds, CancellationToken ct = default);
}

public sealed class TemplateSectionService(IMySqlConnectionFactory connectionFactory) : ITemplateSectionService
{
    public async Task<IReadOnlyList<TemplateSection>> GetAllByTemplateIdAsync(int templateId, CancellationToken ct = default)
    {
        var items = new List<TemplateSection>();
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, template_id, section_code, default_order
            FROM template_sections
            WHERE template_id = @templateId
            ORDER BY default_order ASC, id ASC";
        cmd.Parameters.AddWithValue("@templateId", templateId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(MapSection(reader));

        return items;
    }

    public async Task<TemplateSection?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, template_id, section_code, default_order
            FROM template_sections
            WHERE id = @id
            LIMIT 1";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapSection(reader) : null;
    }

    public async Task<TemplateSection> CreateAsync(CreateTemplateSectionRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_sections (template_id, section_code, default_order)
            VALUES (@templateId, @sectionCode, @defaultOrder);
            SELECT LAST_INSERT_ID();";
        cmd.Parameters.AddWithValue("@templateId", request.TemplateId);
        cmd.Parameters.AddWithValue("@sectionCode", request.SectionCode);
        cmd.Parameters.AddWithValue("@defaultOrder", request.DefaultOrder);

        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return await GetByIdAsync(newId, ct)
            ?? throw new InvalidOperationException("Gagal mengambil data section setelah insert.");
    }

    public async Task<bool> UpdateAsync(int id, UpdateTemplateSectionRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE template_sections
            SET section_code = @sectionCode,
                default_order = @defaultOrder
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@sectionCode", request.SectionCode);
        cmd.Parameters.AddWithValue("@defaultOrder", request.DefaultOrder);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM template_sections WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task ReorderAsync(int templateId, int[] orderedIds, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        for (var i = 0; i < orderedIds.Length; i++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE template_sections
                SET default_order = @order
                WHERE id = @id AND template_id = @templateId";
            cmd.Parameters.AddWithValue("@order", i + 1);
            cmd.Parameters.AddWithValue("@id", orderedIds[i]);
            cmd.Parameters.AddWithValue("@templateId", templateId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static TemplateSection MapSection(System.Data.Common.DbDataReader reader)
    {
        return new TemplateSection
        {
            Id = reader.GetInt32("id"),
            TemplateId = reader.GetInt32("template_id"),
            SectionCode = reader.IsDBNull(reader.GetOrdinal("section_code"))
                ? string.Empty
                : reader.GetString("section_code"),
            DefaultOrder = reader.IsDBNull(reader.GetOrdinal("default_order"))
                ? 0
                : reader.GetInt32("default_order"),
        };
    }
}
