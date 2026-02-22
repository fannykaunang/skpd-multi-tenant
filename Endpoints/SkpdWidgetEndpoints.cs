using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SkpdWidgetEndpoints
{
    public static IEndpointRouteBuilder MapSkpdWidgetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/skpd-widgets")
            .WithTags("SKPD Widgets")
            .RequireAuthorization("CanManageSkpdWidgets");

        group.MapGet("/", async (
            int? skpdId,
            ClaimsPrincipal user,
            ISkpdWidgetService service,
            CancellationToken ct) =>
        {
            var effectiveSkpdId = ResolveEffectiveSkpdId(user, skpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            var items = await service.GetAllBySkpdAsync(effectiveSkpdId.Value, ct);
            return Results.Ok(items);
        });

        group.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            ISkpdWidgetService service,
            CancellationToken ct) =>
        {
            var widget = await service.GetByIdAsync(id, ct);
            if (widget is null) return Results.NotFound();

            // Authorization check
            if (!user.IsSuperAdmin())
            {
                var userSkpdId = user.GetSkpdId();
                if (userSkpdId != widget.SkpdId) return Results.Forbid();
            }

            return Results.Ok(widget);
        });

        group.MapPost("/", async (
            [FromBody] CreateSkpdWidgetRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdWidgetService service,
            IAuditService audit,
            CancellationToken ct) =>
        {
            // Authorization check
            if (!user.IsSuperAdmin())
            {
                var userSkpdId = user.GetSkpdId();
                if (!userSkpdId.HasValue) return Results.Forbid();

                // Override skpdId based on authenticated user
                request.SkpdId = userSkpdId.Value;
            }

            try
            {
                var created = await service.CreateAsync(request, ct);
                
                await audit.LogAsync(
                    user: user,
                    httpContext: httpContext,
                    action: "create",
                    eventType: "skpd_widget.created",
                    entityType: "skpd_widgets",
                    entityId: created.Id,
                    newData: created,
                    ct: ct
                );

                return Results.Created($"/api/v1/skpd-widgets/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateSkpdWidgetRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdWidgetService service,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();

            // Authorization check
            if (!user.IsSuperAdmin())
            {
                var userSkpdId = user.GetSkpdId();
                if (userSkpdId != existing.SkpdId) return Results.Forbid();
            }

            try
            {
                var updated = await service.UpdateAsync(id, request, ct);
                
                await audit.LogAsync(
                    user: user,
                    httpContext: httpContext,
                    action: "update",
                    eventType: "skpd_widget.updated",
                    entityType: "skpd_widgets",
                    entityId: updated.Id,
                    oldData: existing,
                    newData: updated,
                    ct: ct
                );

                return Results.Ok(updated);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdWidgetService service,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();

            // Authorization check
            if (!user.IsSuperAdmin())
            {
                var userSkpdId = user.GetSkpdId();
                if (userSkpdId != existing.SkpdId) return Results.Forbid();
            }

            try
            {
                await service.DeleteAsync(id, ct);
                
                await audit.LogAsync(
                    user: user,
                    httpContext: httpContext,
                    action: "delete",
                    eventType: "skpd_widget.deleted",
                    entityType: "skpd_widgets",
                    entityId: id,
                    oldData: existing,
                    ct: ct
                );

                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }

    private static int? ResolveEffectiveSkpdId(ClaimsPrincipal user, int? requestedSkpdId)
    {
        if (user.IsSuperAdmin())
        {
            // SuperAdmin wajib kirim skpdId kalo mau lihat list per SKPD, kl ga kirim berarti Forbid/Bad Request.
            // Biar gampang, kita wajibkan mereka sampaikan skpdId via query params
            return requestedSkpdId;
        }

        // Kalau bukan superadmin, skip requestedSkpdId dan pakai credential auth dari token
        return user.GetSkpdId();
    }
}
