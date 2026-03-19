using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class TemplateSectionEndpoints
{
    public static IEndpointRouteBuilder MapTemplateSectionEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Public endpoint (no auth) ─────────────────────────────────────────
        app.MapGet("/api/v1/template-sections/public", async (
            int templateId,
            ITemplateSectionService service,
            CancellationToken ct) =>
        {
            var items = await service.GetAllByTemplateIdAsync(templateId, ct);
            var sorted = items.OrderBy(s => s.DefaultOrder).ThenBy(s => s.Id);
            return Results.Ok(sorted);
        })
        .WithTags("Template Sections")
        .RequireRateLimiting("PublicPolicy")
        .AllowAnonymous();

        // ── Protected endpoints ───────────────────────────────────────────────
        var group = app.MapGroup("/api/v1/template-sections")
            .WithTags("Template Sections")
            .RequireAuthorization("CanManageTemplates");

        // GET all sections by templateId
        group.MapGet("/", async (
            int templateId,
            ITemplateSectionService service,
            CancellationToken ct) =>
        {
            var items = await service.GetAllByTemplateIdAsync(templateId, ct);
            return Results.Ok(new { status = "success", data = items });
        });

        // GET by id
        group.MapGet("/{id:int}", async (
            int id,
            ITemplateSectionService service,
            CancellationToken ct) =>
        {
            var item = await service.GetByIdAsync(id, ct);
            return item is null
                ? Results.NotFound(new { message = "Section tidak ditemukan." })
                : Results.Ok(new { status = "success", data = item });
        });

        // POST create
        group.MapPost("/", async (
            [FromBody] CreateTemplateSectionRequest request,
            System.Security.Claims.ClaimsPrincipal user,
            HttpContext httpContext,
            ITemplateSectionService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.SectionCode))
                return Results.BadRequest(new { message = "sectionCode wajib diisi." });

            if (request.TemplateId <= 0)
                return Results.BadRequest(new { message = "templateId wajib diisi." });

            request.SectionCode = request.SectionCode.Trim();

            var created = await service.CreateAsync(request, ct);

            await auditService.LogAsync(user, httpContext,
                "CREATE_TEMPLATE_SECTION", "template.sections.create", "template_sections", created.Id,
                "success", "created", oldData: null, newData: created, ct: ct);

            return Results.Created($"/api/v1/template-sections/{created.Id}", new
            {
                status = "success",
                message = "Section berhasil dibuat.",
                data = created
            });
        });

        // PUT update
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateTemplateSectionRequest request,
            System.Security.Claims.ClaimsPrincipal user,
            HttpContext httpContext,
            ITemplateSectionService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.SectionCode))
                return Results.BadRequest(new { message = "sectionCode wajib diisi." });

            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            request.SectionCode = request.SectionCode.Trim();

            var updated = await service.UpdateAsync(id, request, ct);
            if (!updated)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            var newData = await service.GetByIdAsync(id, ct);
            await auditService.LogAsync(user, httpContext,
                "UPDATE_TEMPLATE_SECTION", "template.sections.update", "template_sections", id,
                "success", "updated", oldData: existing, newData: newData, ct: ct);

            return Results.Ok(new { status = "success", message = "Section berhasil diperbarui." });
        });

        // DELETE
        group.MapDelete("/{id:int}", async (
            int id,
            System.Security.Claims.ClaimsPrincipal user,
            HttpContext httpContext,
            ITemplateSectionService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            var deleted = await service.DeleteAsync(id, ct);
            if (!deleted)
                return Results.NotFound(new { message = "Section tidak ditemukan." });

            await auditService.LogAsync(user, httpContext,
                "DELETE_TEMPLATE_SECTION", "template.sections.delete", "template_sections", id,
                "success", "deleted", oldData: existing, newData: null, ct: ct);

            return Results.Ok(new { status = "success", message = "Section berhasil dihapus." });
        });

        // PUT reorder
        group.MapPut("/reorder", async (
            [FromBody] ReorderTemplateSectionsRequest request,
            [FromQuery] int templateId,
            System.Security.Claims.ClaimsPrincipal user,
            HttpContext httpContext,
            ITemplateSectionService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (request.SectionIds.Length == 0)
                return Results.BadRequest(new { message = "sectionIds tidak boleh kosong." });

            if (templateId <= 0)
                return Results.BadRequest(new { message = "templateId wajib diisi." });

            await service.ReorderAsync(templateId, request.SectionIds, ct);

            await auditService.LogAsync(user, httpContext,
                "REORDER_TEMPLATE_SECTIONS", "template.sections.reorder", "template_sections", null,
                "success", "reordered", oldData: null, newData: new { templateId, request.SectionIds }, ct: ct);

            return Results.Ok(new { status = "success", message = "Urutan section berhasil disimpan." });
        });

        return app;
    }
}
