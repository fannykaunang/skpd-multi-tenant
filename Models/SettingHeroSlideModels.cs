namespace skpd_multi_tenant_api.Models;

public sealed class SettingHeroSlide
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
    public string ButtonTarget { get; set; } = "_self";
    public string TextAlign { get; set; } = "middle-center";
    public decimal OverlayOpacity { get; set; } = 0.50m;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class UpsertSettingHeroSlideRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
    public string ButtonTarget { get; set; } = "_self";
    public string TextAlign { get; set; } = "middle-center";
    public decimal OverlayOpacity { get; set; } = 0.50m;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ReorderSettingHeroSlidesRequest
{
    public int[] Ids { get; set; } = [];
}
