namespace skpd_multi_tenant_api.Models;

public sealed class Template
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviewImage { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviewImage { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviewImage { get; set; }
    public bool IsActive { get; set; } = true;
}
