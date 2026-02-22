using System.ComponentModel.DataAnnotations;

namespace skpd_multi_tenant_api.Models;

public class SkpdWidget
{
    public int Id { get; set; }
    public int SkpdId { get; set; }
    public string? WidgetCode { get; set; }
    public string? WidgetType { get; set; }
    public string? Config { get; set; } // JSON
    public bool IsActive { get; set; }

    // Navigation property for response
    public Skpd? Skpd { get; set; }
}

public class CreateSkpdWidgetRequest
{
    [Required]
    public int SkpdId { get; set; }

    [Required]
    [MaxLength(100)]
    public string WidgetCode { get; set; } = string.Empty;

    [Required]
    public string WidgetType { get; set; } = string.Empty;

    public string? Config { get; set; } // Raw JSON
    
    public bool IsActive { get; set; } = true;
}

public class UpdateSkpdWidgetRequest
{
    [Required]
    [MaxLength(100)]
    public string WidgetCode { get; set; } = string.Empty;

    [Required]
    public string WidgetType { get; set; } = string.Empty;

    public string? Config { get; set; } // Raw JSON
    
    public bool IsActive { get; set; } = true;
}
