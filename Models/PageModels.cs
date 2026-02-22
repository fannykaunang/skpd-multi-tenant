namespace skpd_multi_tenant_api.Models;

public sealed class Page
{
    public long Id { get; set; }
    public int SkpdId { get; set; }
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Content { get; set; }
    public string Status { get; set; } = "draft";
    public long? CreatedBy { get; set; }
    public string? CreatedByName { get; set; } // Join result
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class CreatePageRequest
{
    public int? SkpdId { get; set; }
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Content { get; set; }
    public string Status { get; set; } = "draft";
}

public sealed class UpdatePageRequest
{
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Content { get; set; }
    public string Status { get; set; } = "draft";
}

public sealed class PageQueryParams
{
    public int? SkpdId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class PageListResponse
{
    public IReadOnlyList<Page> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
