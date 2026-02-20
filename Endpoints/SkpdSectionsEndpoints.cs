using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SkpdSectionsEndpoints
{
    public static IEndpointRouteBuilder MapSkpdSectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/skpd-sections")
            .WithTags("SKPD Sections")
            .RequireAuthorization("CanManageSkpdSections");

        group.MapGet("/", async (
            int? skpdId,
            ClaimsPrincipal user,
            ISkpdSectionsService service,
            CancellationToken ct) =>
        {
            var effectiveSkpdId = ResolveEffectiveSkpdId(user, skpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            var items = await service.GetAllAsync(effectiveSkpdId.Value, ct);
            return Results.Ok(new { status = "success", data = items });
        });

        group.MapPost("/", async (
            [FromBody] CreateSkpdSectionRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdSectionsService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.SectionCode))
                return Results.BadRequest(new { message = "sectionCode wajib diisi." });

            var effectiveSkpdId = ResolveEffectiveSkpdId(user, request.SkpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            request.SectionCode = request.SectionCode.Trim();
            var created = await service.CreateAsync(effectiveSkpdId.Value, request, ct);

            await auditService.LogAsync(user, httpContext,
                "CREATE_SKPD_SECTION", "skpd.sections.create", "skpd_sections", created.Id,
                "success", "created", oldData: null, newData: created, ct: ct);

            return Results.Created($"/api/v1/skpd-sections/{created.Id}", new
            {
                status = "success",
                message = "Section berhasil dibuat.",
                data = created
            });
        });

        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateSkpdSectionRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdSectionsService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.SectionCode))
                return Results.BadRequest(new { message = "sectionCode wajib diisi." });

            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            if (!CanAccessSkpd(user, existing.SkpdId))
                return Results.Forbid();

            request.SectionCode = request.SectionCode.Trim();
            var updated = await service.UpdateAsync(id, request, ct);
            if (!updated)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            var newData = await service.GetByIdAsync(id, ct);
            await auditService.LogAsync(user, httpContext,
                "UPDATE_SKPD_SECTION", "skpd.sections.update", "skpd_sections", id,
                "success", "updated", oldData: existing, newData: newData, ct: ct);

            return Results.Ok(new { status = "success", message = "Section berhasil diperbarui." });
        });

        group.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdSectionsService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            if (!CanAccessSkpd(user, existing.SkpdId))
                return Results.Forbid();

            var deleted = await service.DeleteAsync(id, ct);
            if (!deleted)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            await auditService.LogAsync(user, httpContext,
                "DELETE_SKPD_SECTION", "skpd.sections.delete", "skpd_sections", id,
                "success", "deleted", oldData: existing, newData: null, ct: ct);

            return Results.Ok(new { status = "success", message = "Section berhasil dihapus." });
        });

        // Drag-drop reorder
        group.MapPut("/reorder", async (
            [FromBody] ReorderSkpdSectionsRequest request,
            int? skpdId,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdSectionsService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (request.SectionIds.Count == 0)
                return Results.BadRequest(new { message = "sectionIds tidak boleh kosong." });

            var effectiveSkpdId = ResolveEffectiveSkpdId(user, skpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            var ok = await service.ReorderAsync(effectiveSkpdId.Value, request.SectionIds, ct);
            if (!ok)
                return Results.BadRequest(new { message = "Sebagian section tidak valid untuk SKPD ini." });

            await auditService.LogAsync(user, httpContext,
                "REORDER_SKPD_SECTIONS", "skpd.sections.reorder", "skpd_sections", null,
                "success", "reordered", oldData: null, newData: new { effectiveSkpdId, request.SectionIds }, ct: ct);

            return Results.Ok(new { status = "success", message = "Urutan section berhasil diperbarui." });
        });

        return app;
    }

    private static int? ResolveEffectiveSkpdId(ClaimsPrincipal user, int? requestedSkpdId)
    {
        if (user.IsSuperAdmin())
            return requestedSkpdId;

        return user.GetSkpdId();
    }

    private static bool CanAccessSkpd(ClaimsPrincipal user, int skpdId)
    {
        if (user.IsSuperAdmin()) return true;
        return user.GetSkpdId() == skpdId;
    }
}
