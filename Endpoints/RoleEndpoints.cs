using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/roles")
            .WithTags("Roles")
            .RequireAuthorization();

        // ── GET all roles ─────────────────────────────────────────────────────
        // SuperAdmin: semua roles. Admin (assign_role): hanya roles milik SKPD-nya.
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IRoleService service,
            CancellationToken ct) =>
        {
            int? filterSkpdId = null;
            if (!user.IsSuperAdmin())
            {
                filterSkpdId = user.GetSkpdId();
                if (!filterSkpdId.HasValue)
                    return Results.Forbid();
            }

            var roles = await service.GetAllAsync(filterSkpdId, ct);
            return Results.Ok(roles);
        })
        .RequireAuthorization("CanViewRoles");

        // ── GET role by id ────────────────────────────────────────────────────
        group.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            IRoleService service,
            CancellationToken ct) =>
        {
            var role = await service.GetByIdAsync(id, ct);
            if (role is null)
                return Results.NotFound(new { status = "error", message = "Role tidak ditemukan" });

            // Non-SuperAdmin hanya bisa lihat role milik SKPD-nya
            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || role.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            return Results.Ok(role);
        })
        .RequireAuthorization("CanViewRoles");

        // ── GET all permissions (for SuperAdmin to assign to roles) ───────────
        group.MapGet("/permissions/all", async (
            IRoleService service,
            CancellationToken ct) =>
        {
            var permissions = await service.GetAllPermissionsAsync(ct);
            return Results.Ok(permissions);
        })
        .RequireAuthorization("CanViewRoles");

        // ── POST create role ─────────────────────────────────────────────────
        // SuperAdmin: bebas pilih SKPD dan permissions.
        // Admin (assign_role): hanya untuk SKPD-nya, tidak bisa set permissions.
        group.MapPost("/", async (
            [FromBody] CreateRoleRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IRoleService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                // Paksa ke SKPD milik user
                request.SkpdId = skpdId.Value;
                // Non-SuperAdmin tidak bisa set permissions langsung
                request.PermissionIds.Clear();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { status = "error", message = "Nama role wajib diisi" });

            try
            {
                var created = await service.CreateAsync(request, ct);
                await auditService.LogAsync(user, httpContext,
                    "CREATE_ROLE", "role.create", "role", created.Id,
                    "success", "created", oldData: null, newData: created, ct);
                return Results.Created($"/api/v1/roles/{created.Id}", new
                {
                    status = "success",
                    message = "Role berhasil dibuat",
                    data = created
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { status = "error", message = ex.Message });
            }
        })
        .RequireAuthorization("CanManageRoles");

        // ── PUT update role (name & description) ─────────────────────────────
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateRoleRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IRoleService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Role tidak ditemukan" });

            // Non-SuperAdmin hanya bisa edit role di SKPD-nya dan bukan role global
            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { status = "error", message = "Nama role wajib diisi" });

            var updated = await service.UpdateAsync(id, request, ct);
            if (!updated)
                return Results.NotFound(new { status = "error", message = "Gagal memperbarui role" });

            await auditService.LogAsync(user, httpContext,
                "UPDATE_ROLE", "role.update", "role", id,
                "success", "updated", oldData: existing, newData: request, ct);
            return Results.Ok(new
            {
                status = "success",
                message = "Role berhasil diperbarui",
                data = new { id }
            });
        })
        .RequireAuthorization("CanManageRoles");

        // ── PUT update role permissions (SuperAdmin only) ─────────────────────
        group.MapPut("/{id:int}/permissions", async (
            int id,
            [FromBody] UpdateRolePermissionsRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IRoleService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            // Fetch existing untuk audit — simpan permissions sebelum diubah
            var existingRole = await service.GetByIdAsync(id, ct);
            if (existingRole is null)
                return Results.NotFound(new { status = "error", message = "Role tidak ditemukan" });

            var updated = await service.UpdatePermissionsAsync(id, request.PermissionIds, ct);
            if (!updated)
                return Results.NotFound(new { status = "error", message = "Role tidak ditemukan" });

            await auditService.LogAsync(user, httpContext,
                "UPDATE_ROLE_PERMISSIONS", "role.update_permissions", "role", id,
                "success", "permissions_updated",
                oldData: new { permissions = existingRole.Permissions },
                newData: new { permissionIds = request.PermissionIds }, ct);
            return Results.Ok(new
            {
                status = "success",
                message = "Permission role berhasil diperbarui",
                data = new { id }
            });
        })
        .RequireAuthorization("ManageAll");

        // ── DELETE role (SuperAdmin only) ─────────────────────────────────────
        group.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IRoleService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            // Fetch existing untuk audit sebelum dihapus
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Role tidak ditemukan" });

            try
            {
                var deleted = await service.DeleteAsync(id, ct);
                if (!deleted)
                    return Results.NotFound(new { status = "error", message = "Role tidak ditemukan" });

                await auditService.LogAsync(user, httpContext,
                    "DELETE_ROLE", "role.delete", "role", id,
                    "success", "deleted", oldData: existing, newData: null, ct);
                return Results.Ok(new
                {
                    status = "success",
                    message = "Role berhasil dihapus",
                    data = new { id }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { status = "error", message = ex.Message });
            }
        })
        .RequireAuthorization("ManageAll");

        return app;
    }
}
