using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SkpdEndpoints
{
    public static IEndpointRouteBuilder MapSkpdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/skpd")
            .WithTags("SKPD")
            .RequireAuthorization();

        group.MapGet("/", async (ISkpdService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(cancellationToken)));

        group.MapGet("/{id:int}", async (int id, ISkpdService service, CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("/", async (
            [FromBody] CreateSkpdRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request, cancellationToken);
            await auditService.LogAsync(user, httpContext,
                "CREATE_SKPD", "skpd.create", "skpd", created.Id,
                "success", "created", oldData: null, newData: created, cancellationToken);
            return Results.Created($"/api/v1/skpd/{created.Id}", new
            {
                status = "success",
                message = "SKPD berhasil dibuat",
                data = created
            });
        }).RequireAuthorization("CanCreateSkpd");

        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateSkpdRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "SKPD tidak ditemukan" });

            var updated = await service.UpdateAsync(id, request, cancellationToken);
            if (!updated)
                return Results.NotFound(new { status = "error", message = "SKPD tidak ditemukan" });

            await auditService.LogAsync(user, httpContext,
                "UPDATE_SKPD", "skpd.update", "skpd", id,
                "success", "updated", oldData: existing, newData: request, cancellationToken);
            return Results.Ok(new
            {
                status = "success",
                message = "SKPD berhasil diperbarui",
                data = new { id }
            });
        }).RequireAuthorization("CanEditSkpd");

        group.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "SKPD tidak ditemukan" });

            var deleted = await service.DeleteAsync(id, cancellationToken);
            if (!deleted)
                return Results.NotFound(new { status = "error", message = "SKPD tidak ditemukan" });

            await auditService.LogAsync(user, httpContext,
                "DELETE_SKPD", "skpd.delete", "skpd", id,
                "success", "deleted", oldData: existing, newData: null, cancellationToken);
            return Results.Ok(new
            {
                status = "success",
                message = "SKPD berhasil dihapus",
                data = new { id }
            });
        }).RequireAuthorization("CanDeleteSkpd");

        return app;
    }
}
