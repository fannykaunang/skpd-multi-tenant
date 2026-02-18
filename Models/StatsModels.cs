namespace skpd_multi_tenant_api.Models;

public class DashboardStats
{
    public SkpdStats Skpd { get; set; } = new();
    public BeritaStats Berita { get; set; } = new();
    public UserStats Users { get; set; } = new();
    public int TotalKategori { get; set; }
    public List<MonthlyBeritaCount> BeritaPerBulan { get; set; } = new();
    public List<RecentAuditActivity> RecentActivity { get; set; } = new();
}

public class SkpdStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class BeritaStats
{
    public int Total { get; set; }
    public int Published { get; set; }
    public int Draft { get; set; }
    public int Review { get; set; }
    public long TotalViews { get; set; }
}

public class UserStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class MonthlyBeritaCount
{
    public string Month { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Published { get; set; }
}

public class RecentAuditActivity
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Identity { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
