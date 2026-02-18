using System.Security.Claims;

namespace skpd_multi_tenant_api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Get User ID from JWT token (sub claim)
    /// </summary>
    public static long? GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value
                       ?? user.FindFirst("userId")?.Value;

        return long.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Get SKPD ID from JWT token (skpd_id claim)
    /// </summary>
    public static int? GetSkpdId(this ClaimsPrincipal user)
    {
        var skpdIdClaim = user.FindFirst("skpd_id")?.Value;

        return int.TryParse(skpdIdClaim, out var skpdId) ? skpdId : null;
    }

    /// <summary>
    /// Get Username from JWT token (unique_name claim)
    /// </summary>
    public static string? GetUsername(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("unique_name")?.Value;
    }

    /// <summary>
    /// Get Role from JWT token (role claim)
    /// </summary>
    public static string? GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Role)?.Value
            ?? user.FindFirst("role")?.Value;
    }

    /// <summary>
    /// Check if user is Admin
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        return user.IsInRole("Admin");
    }

    /// <summary>
    /// Check if user is Operator
    /// </summary>
    public static bool IsOperator(this ClaimsPrincipal user)
    {
        return user.IsInRole("Operator");
    }

    /// <summary>
    /// Check if user can access specific SKPD
    /// Admin can access all SKPD, Operator can only access their own SKPD
    /// </summary>
    public static bool CanAccessSkpd(this ClaimsPrincipal user, int skpdId)
    {
        if (user.IsAdmin() || user.IsSuperAdmin())
            return true;

        var userSkpdId = user.GetSkpdId();
        return userSkpdId.HasValue && userSkpdId.Value == skpdId;
    }

    public static bool HasPermission(this ClaimsPrincipal user, string permission)
    {
        var permissions = user.FindAll("permission").Select(c => c.Value);
        return permissions.Contains(permission) || permissions.Contains("manage_all");
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
    {
        return user.FindAll("permission").Any(c => c.Value == "manage_all");
    }

    public static IEnumerable<string> GetPermissions(this ClaimsPrincipal user)
    {
        return user.FindAll("permission").Select(c => c.Value);
    }
}