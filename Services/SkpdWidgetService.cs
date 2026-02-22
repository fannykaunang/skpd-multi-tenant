using System.Data;
using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISkpdWidgetService
{
    Task<IEnumerable<SkpdWidget>> GetAllBySkpdAsync(int skpdId, CancellationToken ct = default);
    Task<SkpdWidget?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SkpdWidget> CreateAsync(CreateSkpdWidgetRequest request, CancellationToken ct = default);
    Task<SkpdWidget> UpdateAsync(int id, UpdateSkpdWidgetRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class SkpdWidgetService(IMySqlConnectionFactory connectionFactory) : ISkpdWidgetService
{
    public async Task<IEnumerable<SkpdWidget>> GetAllBySkpdAsync(int skpdId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT w.id, w.skpd_id, w.widget_code, w.widget_type, w.config, w.is_active,
                   s.name as skpd_name
            FROM skpd_widgets w
            JOIN skpd s ON w.skpd_id = s.id
            WHERE w.skpd_id = @skpdId
            ORDER BY w.id ASC";
            
        var param = cmd.CreateParameter();
        param.ParameterName = "@skpdId";
        param.Value = skpdId;
        cmd.Parameters.Add(param);
            
        var items = new List<SkpdWidget>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapToSkpdWidget(reader, includeSkpdName: true));
        }
        return items;
    }

    public async Task<SkpdWidget?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, skpd_id, widget_code, widget_type, config, is_active 
            FROM skpd_widgets 
            WHERE id = @id";
        
        var param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = id;
        cmd.Parameters.Add(param);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapToSkpdWidget(reader, includeSkpdName: false);
        }
        return null;
    }

    public async Task<SkpdWidget> CreateAsync(CreateSkpdWidgetRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO skpd_widgets (skpd_id, widget_code, widget_type, config, is_active)
            VALUES (@skpd_id, @widget_code, @widget_type, @config, @is_active);
            SELECT LAST_INSERT_ID();";

        var pSkpdId = cmd.CreateParameter();
        pSkpdId.ParameterName = "@skpd_id";
        pSkpdId.Value = request.SkpdId;
        cmd.Parameters.Add(pSkpdId);

        var pCode = cmd.CreateParameter();
        pCode.ParameterName = "@widget_code";
        pCode.Value = request.WidgetCode;
        cmd.Parameters.Add(pCode);

        var pType = cmd.CreateParameter();
        pType.ParameterName = "@widget_type";
        pType.Value = request.WidgetType;
        cmd.Parameters.Add(pType);

        var pConfig = cmd.CreateParameter();
        pConfig.ParameterName = "@config";
        pConfig.Value = request.Config ?? (object)DBNull.Value;
        cmd.Parameters.Add(pConfig);

        var pActive = cmd.CreateParameter();
        pActive.ParameterName = "@is_active";
        pActive.Value = request.IsActive ? 1 : 0;
        cmd.Parameters.Add(pActive);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return await GetByIdAsync(id, ct) ?? throw new Exception("Gagal membuat widget.");
    }

    public async Task<SkpdWidget> UpdateAsync(int id, UpdateSkpdWidgetRequest request, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        
        cmd.CommandText = @"
            UPDATE skpd_widgets 
            SET widget_code = @widget_code, widget_type = @widget_type, config = @config, is_active = @is_active 
            WHERE id = @id";

        var pId = cmd.CreateParameter();
        pId.ParameterName = "@id";
        pId.Value = id;
        cmd.Parameters.Add(pId);

        var pCode = cmd.CreateParameter();
        pCode.ParameterName = "@widget_code";
        pCode.Value = request.WidgetCode;
        cmd.Parameters.Add(pCode);

        var pType = cmd.CreateParameter();
        pType.ParameterName = "@widget_type";
        pType.Value = request.WidgetType;
        cmd.Parameters.Add(pType);

        var pConfig = cmd.CreateParameter();
        pConfig.ParameterName = "@config";
        pConfig.Value = request.Config ?? (object)DBNull.Value;
        cmd.Parameters.Add(pConfig);

        var pActive = cmd.CreateParameter();
        pActive.ParameterName = "@is_active";
        pActive.Value = request.IsActive ? 1 : 0;
        cmd.Parameters.Add(pActive);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
        {
            throw new Exception("Widget tidak ditemukan.");
        }

        return await GetByIdAsync(id, ct) ?? throw new Exception("Gagal update widget.");
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        
        cmd.CommandText = "DELETE FROM skpd_widgets WHERE id = @id";
        var pid = cmd.CreateParameter();
        pid.ParameterName = "@id";
        pid.Value = id;
        cmd.Parameters.Add(pid);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static SkpdWidget MapToSkpdWidget(IDataReader reader, bool includeSkpdName)
    {
        var widget = new SkpdWidget
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            SkpdId = reader.GetInt32(reader.GetOrdinal("skpd_id")),
            WidgetCode = reader.IsDBNull(reader.GetOrdinal("widget_code")) ? null : reader.GetString(reader.GetOrdinal("widget_code")),
            WidgetType = reader.IsDBNull(reader.GetOrdinal("widget_type")) ? null : reader.GetString(reader.GetOrdinal("widget_type")),
            Config = reader.IsDBNull(reader.GetOrdinal("config")) ? null : reader.GetString(reader.GetOrdinal("config")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
        };

        if (includeSkpdName && !reader.IsDBNull(reader.GetOrdinal("skpd_name")))
        {
            widget.Skpd = new Skpd { Nama = reader.GetString(reader.GetOrdinal("skpd_name")) };
        }

        return widget;
    }
}
