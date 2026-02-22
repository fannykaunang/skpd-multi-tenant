namespace skpd_multi_tenant_api.Models;

public sealed class SkpdTemplateSetting
{
    public int Id { get; set; }
    public int SkpdId { get; set; }
    public int TemplateId { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FontFamily { get; set; }
    public string? HeaderStyle { get; set; }
    public string? FooterStyle { get; set; }
    public string? HeroLayout { get; set; }
    public string? CustomCss { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class TemplateOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviewImage { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpsertSkpdTemplateRequest
{
    public int? SkpdId { get; set; }
    public int TemplateId { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FontFamily { get; set; }
    public string? HeaderStyle { get; set; }
    public string? FooterStyle { get; set; }
    public string? HeroLayout { get; set; }
    public string? CustomCss { get; set; }
}
