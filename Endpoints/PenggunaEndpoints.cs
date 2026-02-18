using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class PenggunaEndpoints
{
    public static IEndpointRouteBuilder MapPenggunaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pengguna")
            .WithTags("Pengguna")
            .RequireAuthorization();

        // GET all — SuperAdmin: semua pengguna, Admin: hanya pengguna di SKPD-nya
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IPenggunaService service,
            CancellationToken cancellationToken) =>
        {
            if (user.IsSuperAdmin())
            {
                return Results.Ok(await service.GetAllAsync(cancellationToken));
            }

            var skpdId = user.GetSkpdId();
            if (!skpdId.HasValue)
                return Results.Forbid();

            return Results.Ok(await service.GetAllBySkpdAsync(skpdId.Value, cancellationToken));
        }).RequireAuthorization("CanViewUsers");

        // GET by ID — SuperAdmin: bebas, Admin: hanya pengguna di SKPD-nya
        group.MapGet("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            IPenggunaService service,
            CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            if (item is null)
                return Results.NotFound(new { status = "error", message = "Pengguna tidak ditemukan" });

            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || item.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            return Results.Ok(item);
        }).RequireAuthorization("CanViewUsers");

        // POST create — SuperAdmin: bebas, Admin: hanya bisa buat pengguna di SKPD-nya
        group.MapPost("/", async (
            [FromBody] CreatePenggunaRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPenggunaService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                // Force skpdId ke SKPD milik user
                request.SkpdId = skpdId.Value;
            }

            var created = await service.CreateAsync(request, cancellationToken);
            await auditService.LogAsync(user, httpContext,
                "CREATE_PENGGUNA", "pengguna.create", "pengguna", created.Id,
                "success", "created", oldData: null, newData: created, cancellationToken);
            return Results.Created($"/api/v1/pengguna/{created.Id}", new
            {
                status = "success",
                message = "Pengguna berhasil dibuat",
                data = created
            });
        }).RequireAuthorization("CanCreateUser");

        // PUT update — SuperAdmin: bebas, Admin: hanya pengguna di SKPD-nya
        group.MapPut("/{id:long}", async (
            long id,
            [FromBody] UpdatePenggunaRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPenggunaService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            // Fetch existing untuk audit (sekaligus dipakai untuk SKPD scope check)
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Pengguna tidak ditemukan" });

            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                if (existing.SkpdId != skpdId.Value)
                    return Results.Forbid();

                // Force skpdId ke SKPD milik user
                request.SkpdId = skpdId.Value;
            }

            var updated = await service.UpdateAsync(id, request, cancellationToken);
            if (!updated)
            {
                return Results.NotFound(new { status = "error", message = "Pengguna tidak ditemukan" });
            }
            await auditService.LogAsync(user, httpContext,
                "UPDATE_PENGGUNA", "pengguna.update", "pengguna", id,
                "success", "updated", oldData: existing, newData: request, cancellationToken);
            return Results.Ok(new
            {
                status = "success",
                message = "Pengguna berhasil diperbarui",
                data = new { id }
            });
        }).RequireAuthorization("CanEditUser");

        // DELETE — SuperAdmin: bebas, Admin: hanya pengguna di SKPD-nya
        group.MapDelete("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPenggunaService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            // Fetch existing untuk audit (sekaligus dipakai untuk SKPD scope check)
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Pengguna tidak ditemukan" });

            if (!user.IsSuperAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                if (existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            var deleted = await service.DeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return Results.NotFound(new { status = "error", message = "Pengguna tidak ditemukan" });
            }
            await auditService.LogAsync(user, httpContext,
                "DELETE_PENGGUNA", "pengguna.delete", "pengguna", id,
                "success", "deleted", oldData: existing, newData: null, cancellationToken);
            return Results.Ok(new
            {
                status = "success",
                message = "Pengguna berhasil dihapus",
                data = new { id }
            });
        }).RequireAuthorization("CanDeleteUser");

        // GET roles — SuperAdmin: semua, Admin: hanya roles di SKPD-nya
        group.MapGet("/roles/all", async (
            ClaimsPrincipal user,
            IPenggunaService service,
            CancellationToken cancellationToken) =>
        {
            if (user.IsSuperAdmin())
            {
                return Results.Ok(await service.GetAllRolesAsync(cancellationToken));
            }

            var skpdId = user.GetSkpdId();
            if (!skpdId.HasValue)
                return Results.Forbid();

            return Results.Ok(await service.GetRolesBySkpdAsync(skpdId.Value, cancellationToken));
        }).RequireAuthorization("CanViewUsers");

        return app;
    }
}
