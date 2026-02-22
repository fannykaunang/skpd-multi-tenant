namespace skpd_multi_tenant_api.Models;

public sealed class Notification
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Link { get; set; }
    public string Type { get; set; } = "info";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UnreadCountResponse
{
    public int Count { get; set; }
}

public sealed class NotificationListResponse
{
    public IEnumerable<Notification> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class NotificationStatsResponse
{
    public int Total { get; set; }
    public int Unread { get; set; }
    public int Info { get; set; }
    public int Success { get; set; }
    public int Warning { get; set; }
    public int Error { get; set; }
}

public sealed class DeleteBatchRequest
{
    public long[] Ids { get; set; } = [];
}
