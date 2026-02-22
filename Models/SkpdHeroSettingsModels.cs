namespace skpd_multi_tenant_api.Models;

public sealed class SkpdHeroSettings
{
    public int Id { get; set; }
    public int SkpdId { get; set; }
    public string HeroType { get; set; } = "image";
    public decimal OverlayOpacity { get; set; } = 0.50m;
    public string Height { get; set; } = "500px";
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Populated on GET when hero_type = slider
    public List<SkpdHeroSlide>? Slides { get; set; }
}

public sealed class UpsertSkpdHeroSettingsRequest
{
    public int? SkpdId { get; set; }
    public string HeroType { get; set; } = "image";
    public decimal OverlayOpacity { get; set; } = 0.50m;
    public string Height { get; set; } = "500px";
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SkpdHeroSlide
{
    public int Id { get; set; }
    public int HeroSettingId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
    public string ButtonTarget { get; set; } = "_self";
    public string TextAlign { get; set; } = "center";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class UpsertSkpdHeroSlideRequest
{
    public int HeroSettingId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
    public string ButtonTarget { get; set; } = "_self";
    public string TextAlign { get; set; } = "center";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ReorderSlidesRequest
{
    public int[] Ids { get; set; } = [];
}
