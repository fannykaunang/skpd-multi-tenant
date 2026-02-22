using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SkpdTemplatesEndpoints
{
    public static IEndpointRouteBuilder MapSkpdTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/skpd-templates")
            .WithTags("SKPD Templates")
            .RequireAuthorization("CanManageSkpdTemplates");

        group.MapGet("/", async (
            int? skpdId,
            ClaimsPrincipal user,
            ISkpdTemplatesService service,
            CancellationToken ct) =>
        {
            if (user.IsSuperAdmin() && !skpdId.HasValue)
                return Results.BadRequest(new { message = "SuperAdmin wajib mengirim skpdId." });

            var effectiveSkpdId = ResolveEffectiveSkpdId(user, skpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            var item = await service.GetBySkpdIdAsync(effectiveSkpdId.Value, ct);

            if (item is null)
            {
                return Results.Ok(new SkpdTemplateSetting
                {
                    Id = 0,
                    SkpdId = effectiveSkpdId.Value,
                    TemplateId = 0,
                    PrimaryColor = null,
                    SecondaryColor = null,
                    FontFamily = null,
                    HeaderStyle = null,
                    FooterStyle = null,
                    HeroLayout = null,
                    CustomCss = null
                });
            }

            return Results.Ok(item);
        });

        group.MapGet("/options", async (
            ISkpdTemplatesService service,
            CancellationToken ct) =>
        {
            var items = await service.GetTemplateOptionsAsync(ct);
            return Results.Ok(new { status = "success", data = items });
        });

        group.MapPut("/", async (
            [FromBody] UpsertSkpdTemplateRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdTemplatesService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (request.TemplateId <= 0)
                return Results.BadRequest(new { message = "templateId wajib diisi." });

            if (user.IsSuperAdmin() && !request.SkpdId.HasValue)
                return Results.BadRequest(new { message = "SuperAdmin wajib mengirim skpdId." });

            var effectiveSkpdId = ResolveEffectiveSkpdId(user, request.SkpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            var templateExists = await service.IsTemplateExistsAsync(request.TemplateId, ct);
            if (!templateExists)
                return Results.BadRequest(new { message = "Template tidak ditemukan." });

            var oldData = await service.GetBySkpdIdAsync(effectiveSkpdId.Value, ct);
            var updated = await service.UpsertAsync(effectiveSkpdId.Value, request, ct);

            await auditService.LogAsync(user, httpContext,
                "UPSERT_SKPD_TEMPLATES", "skpd.templates.upsert", "skpd_templates", updated.Id,
                "success", "updated", oldData: oldData, newData: updated, ct: ct);

            return Results.Ok(new
            {
                status = "success",
                message = "Template SKPD berhasil disimpan.",
                data = updated
            });
        });

        return app;
    }

    private static int? ResolveEffectiveSkpdId(ClaimsPrincipal user, int? requestedSkpdId)
    {
        if (user.IsSuperAdmin())
            return requestedSkpdId;

        return user.GetSkpdId();
    }
}
