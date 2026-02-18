namespace skpd_multi_tenant_api.Models;

public sealed class Pengguna
{
    public long Id { get; set; }
    public int? SkpdId { get; set; }
    public string? SkpdNama { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<PenggunaRole> Roles { get; set; } = new();
}

public sealed class PenggunaRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreatePenggunaRequest
{
    public int? SkpdId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<int> RoleIds { get; set; } = new();
}

public sealed class UpdatePenggunaRequest
{
    public int? SkpdId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> RoleIds { get; set; } = new();
}

public sealed class RoleItem
{
    public int Id { get; set; }
    public int? SkpdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
