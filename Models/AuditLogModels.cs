namespace skpd_multi_tenant_api.Models;

public class AuditLogItem
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public int? SkpdId { get; set; }
    public string? SkpdNama { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Identity { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AuditLogListResponse
{
    public List<AuditLogItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
