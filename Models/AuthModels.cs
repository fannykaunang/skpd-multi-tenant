namespace skpd_multi_tenant_api.Models;

public sealed class LoginRequest
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public int? SkpdId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public sealed class AuthUser
{
    public long Id { get; set; }
    public int? SkpdId { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int FailedLoginAttempt { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime? LastFailedLogin { get; set; }

    public DateTime? LastLogin { get; set; }

    public List<string> Roles { get; set; } = new();
}
