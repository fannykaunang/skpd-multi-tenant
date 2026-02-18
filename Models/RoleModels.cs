namespace skpd_multi_tenant_api.Models;

public sealed class RoleDetail
{
    public int Id { get; set; }
    public int? SkpdId { get; set; }
    public string? SkpdNama { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
    public List<PermissionItem> Permissions { get; set; } = new();
}

public sealed class PermissionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class CreateRoleRequest
{
    public int? SkpdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<int> PermissionIds { get; set; } = new();
}

public sealed class UpdateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class UpdateRolePermissionsRequest
{
    public List<int> PermissionIds { get; set; } = new();
}
