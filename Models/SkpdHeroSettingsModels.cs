namespace skpd_multi_tenant_api.Models;

public sealed class SkpdHeroSettings
{
    public int Id { get; set; }
    public int SkpdId { get; set; }
    public string HeroType { get; set; } = "image";
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? BackgroundImage { get; set; }
    public decimal OverlayOpacity { get; set; } = 0.50m;
}

public sealed class UpsertSkpdHeroSettingsRequest
{
    public int? SkpdId { get; set; }
    public string HeroType { get; set; } = "image";
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? BackgroundImage { get; set; }
    public decimal OverlayOpacity { get; set; } = 0.50m;
}
