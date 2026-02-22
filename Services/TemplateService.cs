using System.Data;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ITemplateService
{
    Task<IEnumerable<Template>> GetAllAsync(CancellationToken ct = default);
    Task<Template?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Template> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default);
    Task<Template> UpdateAsync(int id, UpdateTemplateRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class TemplateService(IMySqlConnectionFactory connectionFactory) : ITemplateService
{
    public async Task<IEnumerable<Template>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, code, description, preview_image, is_active, created_at 
            FROM templates 
            ORDER BY name ASC";
            
        var items = new List<Template>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapToTemplate(reader));
        }
        return items;
    }

    public async Task<Template?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, code, description, preview_image, is_active, created_at 
            FROM templates 
            WHERE id = @id";
        
        var param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = id;
        cmd.Parameters.Add(param);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapToTemplate(reader);
        }
        return null;
    }

    public async Task<Template> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM templates WHERE code = @code";
        
        var pcode = cmd.CreateParameter();
        pcode.ParameterName = "@code";
        pcode.Value = request.Code;
        cmd.Parameters.Add(pcode);

        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (exists > 0)
        {
            throw new Exception("Template dengan kode tersebut sudah ada.");
        }

        cmd.CommandText = @"
            INSERT INTO templates (name, code, description, preview_image, is_active, created_at)
            VALUES (@name, @code, @description, @preview_image, @is_active, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        cmd.Parameters.Clear();
        cmd.Parameters.Add(pcode);

        var pname = cmd.CreateParameter();
        pname.ParameterName = "@name";
        pname.Value = request.Name;
        cmd.Parameters.Add(pname);

        var pdesc = cmd.CreateParameter();
        pdesc.ParameterName = "@description";
        pdesc.Value = request.Description ?? (object)DBNull.Value;
        cmd.Parameters.Add(pdesc);

        var pprev = cmd.CreateParameter();
        pprev.ParameterName = "@preview_image";
        pprev.Value = request.PreviewImage ?? (object)DBNull.Value;
        cmd.Parameters.Add(pprev);

        var pactive = cmd.CreateParameter();
        pactive.ParameterName = "@is_active";
        pactive.Value = request.IsActive;
        cmd.Parameters.Add(pactive);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return await GetByIdAsync(id, ct) ?? throw new Exception("Gagal membuat template.");
    }

    public async Task<Template> UpdateAsync(int id, UpdateTemplateRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM templates WHERE code = @code AND id != @id";
        
        var pcode = cmd.CreateParameter();
        pcode.ParameterName = "@code";
        pcode.Value = request.Code;
        cmd.Parameters.Add(pcode);

        var pid = cmd.CreateParameter();
        pid.ParameterName = "@id";
        pid.Value = id;
        cmd.Parameters.Add(pid);

        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (exists > 0)
        {
            throw new Exception("Template dengan kode tersebut sudah ada di entri lain.");
        }

        cmd.CommandText = @"
            UPDATE templates 
            SET name = @name, code = @code, description = @description, preview_image = @preview_image, is_active = @is_active 
            WHERE id = @id";

        cmd.Parameters.Clear();
        cmd.Parameters.Add(pid);
        cmd.Parameters.Add(pcode);

        var pname = cmd.CreateParameter();
        pname.ParameterName = "@name";
        pname.Value = request.Name;
        cmd.Parameters.Add(pname);

        var pdesc = cmd.CreateParameter();
        pdesc.ParameterName = "@description";
        pdesc.Value = request.Description ?? (object)DBNull.Value;
        cmd.Parameters.Add(pdesc);

        var pprev = cmd.CreateParameter();
        pprev.ParameterName = "@preview_image";
        pprev.Value = request.PreviewImage ?? (object)DBNull.Value;
        cmd.Parameters.Add(pprev);

        var pactive = cmd.CreateParameter();
        pactive.ParameterName = "@is_active";
        pactive.Value = request.IsActive;
        cmd.Parameters.Add(pactive);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
        {
            throw new Exception("Template tidak ditemukan.");
        }

        return await GetByIdAsync(id, ct) ?? throw new Exception("Gagal update template.");
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM skpd_templates WHERE template_id = @id";
        
        var pid = cmd.CreateParameter();
        pid.ParameterName = "@id";
        pid.Value = id;
        cmd.Parameters.Add(pid);

        var usedCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (usedCount > 0)
        {
            throw new Exception("Template tidak dapat dihapus karena sedang digunakan oleh SKPD.");
        }

        cmd.CommandText = "DELETE FROM templates WHERE id = @id";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Template MapToTemplate(IDataReader reader)
    {
        return new Template
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Code = reader.GetString(reader.GetOrdinal("code")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            PreviewImage = reader.IsDBNull(reader.GetOrdinal("preview_image")) ? null : reader.GetString(reader.GetOrdinal("preview_image")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }
}
