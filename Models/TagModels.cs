namespace skpd_multi_tenant_api.Models;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateTagRequest
{
    public string Name { get; set; } = string.Empty;
}
