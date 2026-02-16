namespace skpd_multi_tenant.Models;

public sealed class Category
{
    public int Id { get; set; }
    public int SkpdId { get; set; }
    public string? SkpdNama { get; set; }
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateCategoryRequest
{
    public int SkpdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class CategoryQueryParams
{
    public int? SkpdId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}