namespace skpd_multi_tenant_api.Models;

public sealed class MediaItem
{
    public long Id { get; set; }
    public int SkpdId { get; set; }
    public string SkpdNama { get; set; } = string.Empty;
    public long? UploadedBy { get; set; }
    public string? UploadedByName { get; set; }
    public string? FileName { get; set; }
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? FilePath { get; set; }
    public string? FileType { get; set; }
    public int FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
}
