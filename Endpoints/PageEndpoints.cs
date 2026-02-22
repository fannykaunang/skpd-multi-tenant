using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapPageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pages")
            .WithTags("Pages")
            .RequireAuthorization();

        // GET all pages
        group.MapGet("/", async (
            int? skpdId,
            string? status,
            string? search,
            int page,
            int pageSize,
            ClaimsPrincipal user,
            IPageService service,
            CancellationToken cancellationToken) =>
        {
            var canViewAll = user.IsSuperAdmin() || user.IsAdmin();
            var userSkpdId = user.GetSkpdId();

            // Scope check: Role non-SuperAdmin/Admin only see their own SKPD
            if (!canViewAll)
            {
                if (!userSkpdId.HasValue) return Results.Forbid();
                skpdId = userSkpdId.Value;
            }

            var queryParams = new PageQueryParams
            {
                SkpdId = skpdId,
                Status = status,
                Search = search,
                Page = page > 0 ? page : 1,
                PageSize = pageSize > 0 ? pageSize : 10
            };

            var items = await service.GetAllAsync(queryParams, cancellationToken);
            return Results.Ok(items);
        }).RequireAuthorization("CanViewPages");

        // GET page by id
        group.MapGet("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            IPageService service,
            CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            if (item is null) return Results.NotFound();

            // Scope check
            if (!user.IsSuperAdmin() && !user.IsAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || item.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            return Results.Ok(item);
        }).RequireAuthorization("CanViewPages");

        // POST create page
        group.MapPost("/", async (
            [FromBody] CreatePageRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPageService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            // Set SKPD if not SuperAdmin
            if (!user.IsSuperAdmin() && !user.IsAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue) return Results.Forbid();
                request.SkpdId = skpdId.Value;
            }
            else if (request.SkpdId == null)
            {
                return Results.BadRequest(new { message = "SKPD ID wajib diisi." });
            }

            // Publish permission check
            if (request.Status == "published" && !user.HasPermission("publish_page") && !user.IsSuperAdmin())
            {
                request.Status = "draft";
            }

            var created = await service.CreateAsync(request, userId, cancellationToken);

            await auditService.LogAsync(user, httpContext,
                "CREATE_PAGE", "page.create", "pages", created.Id,
                "success", "created", oldData: null, newData: created, cancellationToken);

            return Results.Created($"/api/v1/pages/{created.Id}", new
            {
                status = "success",
                message = "Halaman berhasil dibuat",
                data = created
            });
        }).RequireAuthorization("CanCreatePage");

        // PUT update page
        group.MapPut("/{id:long}", async (
            long id,
            [FromBody] UpdatePageRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPageService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null) return Results.NotFound();

            // Scope check
            if (!user.IsSuperAdmin() && !user.IsAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            // Publish permission check
            if (request.Status == "published" && existing.Status != "published" && 
                !user.HasPermission("publish_page") && !user.IsSuperAdmin())
            {
                // If trying to publish but doesn't have permission, keep as draft or revert to old status if old status was draft
                request.Status = existing.Status;
            }

            var updated = await service.UpdateAsync(id, request, cancellationToken);
            if (!updated) return Results.Problem("Gagal memperbarui halaman.");

            await auditService.LogAsync(user, httpContext,
                "UPDATE_PAGE", "page.update", "pages", id,
                "success", "updated", oldData: existing, newData: request, cancellationToken);

            return Results.Ok(new
            {
                status = "success",
                message = "Halaman berhasil diperbarui"
            });
        }).RequireAuthorization("CanEditPage");

        // DELETE page
        group.MapDelete("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPageService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null) return Results.NotFound();

            // Scope check
            if (!user.IsSuperAdmin() && !user.IsAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            var deleted = await service.DeleteAsync(id, cancellationToken);
            if (!deleted) return Results.Problem("Gagal menghapus halaman.");

            await auditService.LogAsync(user, httpContext,
                "DELETE_PAGE", "page.delete", "pages", id,
                "success", "deleted", oldData: existing, newData: null, cancellationToken);

            return Results.Ok(new
            {
                status = "success",
                message = "Halaman berhasil dihapus"
            });
        }).RequireAuthorization("CanDeletePage");

        return app;
    }
}
