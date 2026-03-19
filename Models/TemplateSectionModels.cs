namespace skpd_multi_tenant_api.Models;

public sealed class TemplateSection
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public int DefaultOrder { get; set; }
}

public sealed class CreateTemplateSectionRequest
{
    public int TemplateId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public int DefaultOrder { get; set; }
}

public sealed class UpdateTemplateSectionRequest
{
    public string SectionCode { get; set; } = string.Empty;
    public int DefaultOrder { get; set; }
}

public sealed class ReorderTemplateSectionsRequest
{
    public int[] SectionIds { get; set; } = [];
}
