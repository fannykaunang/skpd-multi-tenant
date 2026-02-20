namespace skpd_multi_tenant_api.Models;

public sealed class SkpdSection
{
    public int Id { get; set; }
    public int SkpdId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public string? CustomTitle { get; set; }
}

public sealed class CreateSkpdSectionRequest
{
    public int? SkpdId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public string? CustomTitle { get; set; }
}

public sealed class UpdateSkpdSectionRequest
{
    public string SectionCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public string? CustomTitle { get; set; }
}

public sealed class ReorderSkpdSectionsRequest
{
    public List<int> SectionIds { get; set; } = [];
}
