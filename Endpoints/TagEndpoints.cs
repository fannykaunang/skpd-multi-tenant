using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tags")
            .WithTags("Tags")
            .RequireAuthorization();

        // GET all tags — any authenticated user
        group.MapGet("/", async (
            string? search,
            ITagService service,
            CancellationToken ct) =>
        {
            var items = await service.GetAllAsync(search, ct);
            return Results.Ok(items);
        });

        // GET tag by id — any authenticated user
        group.MapGet("/{id:int}", async (
            int id,
            ITagService service,
            CancellationToken ct) =>
        {
            var item = await service.GetByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        // GET berita by tag id
        group.MapGet("/{id:int}/berita", async (
            int id,
            ITagService service,
            CancellationToken ct) =>
        {
            var items = await service.GetBeritaByTagAsync(id, ct);
            return Results.Ok(items);
        });

        // POST create tag — ManageAll only
        group.MapPost("/", async (
            [FromBody] CreateTagRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ITagService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Validation Error", message = "Nama tag wajib diisi." });

            var created = await service.CreateAsync(request, ct);
            await auditService.LogAsync(user, httpContext,
                "CREATE_TAG", "tag.create", "tag", created.Id,
                "success", "created", oldData: null, newData: created, ct);
            return Results.Created($"/api/v1/tags/{created.Id}", created);
        }).RequireAuthorization("ManageAll");

        // PUT update tag — ManageAll only
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateTagRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ITagService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Validation Error", message = "Nama tag wajib diisi." });

            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();

            var updated = await service.UpdateAsync(id, request, ct);
            if (!updated) return Results.NotFound();

            await auditService.LogAsync(user, httpContext,
                "UPDATE_TAG", "tag.update", "tag", id,
                "success", "updated", oldData: existing, newData: request, ct);
            return Results.NoContent();
        }).RequireAuthorization("ManageAll");

        // DELETE tag — ManageAll only
        group.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ITagService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();

            var deleted = await service.DeleteAsync(id, ct);
            if (!deleted) return Results.NotFound();

            await auditService.LogAsync(user, httpContext,
                "DELETE_TAG", "tag.delete", "tag", id,
                "success", "deleted", oldData: existing, newData: null, ct);
            return Results.NoContent();
        }).RequireAuthorization("ManageAll");

        return app;
    }
}
