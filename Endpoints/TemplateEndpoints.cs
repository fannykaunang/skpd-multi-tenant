using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/templates")
            .WithTags("Templates")
            .RequireAuthorization("CanManageTemplates");

        group.MapGet("/", async (ITemplateService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(cancellationToken)));

        group.MapGet("/{id:int}", async (int id, ITemplateService service, CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("/", async (
            [FromBody] CreateTemplateRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ITemplateService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var created = await service.CreateAsync(request, cancellationToken);
                await auditService.LogAsync(user, httpContext,
                    "CREATE_TEMPLATE", "templates.create", "templates", created.Id,
                    "success", "created", oldData: null, newData: created, cancellationToken);
                return Results.Created($"/api/v1/templates/{created.Id}", new
                {
                    status = "success",
                    message = "Template berhasil dibuat",
                    data = created
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateTemplateRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ITemplateService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var oldData = await service.GetByIdAsync(id, cancellationToken);
                if (oldData is null) return Results.NotFound();

                var updated = await service.UpdateAsync(id, request, cancellationToken);
                await auditService.LogAsync(user, httpContext,
                    "UPDATE_TEMPLATE", "templates.update", "templates", id,
                    "success", "updated", oldData: oldData, newData: updated, cancellationToken);
                return Results.Ok(new
                {
                    status = "success",
                    message = "Template berhasil diperbarui",
                    data = updated
                });
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
            ITemplateService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var oldData = await service.GetByIdAsync(id, cancellationToken);
                if (oldData is null) return Results.NotFound();

                await service.DeleteAsync(id, cancellationToken);
                await auditService.LogAsync(user, httpContext,
                    "DELETE_TEMPLATE", "templates.delete", "templates", id,
                    "success", "deleted", oldData: oldData, newData: null, cancellationToken);
                return Results.Ok(new
                {
                    status = "success",
                    message = "Template berhasil dihapus"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }
}
