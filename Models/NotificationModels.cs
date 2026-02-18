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
