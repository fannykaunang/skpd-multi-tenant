namespace skpd_multi_tenant_api.Models;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
}

public sealed class UpdateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
}
