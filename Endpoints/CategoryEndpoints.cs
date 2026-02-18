using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories")
            .WithTags("Categories")
            .RequireAuthorization();

        // GET all categories — SuperAdmin: semua, user lain: hanya kategori di SKPD-nya
        group.MapGet("/", async (
            int? skpdId,
            int page,
            int pageSize,
            ClaimsPrincipal user,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            // Set default values
            page = page > 0 ? page : 1;
            pageSize = pageSize > 0 ? pageSize : 10;

            // SuperAdmin bisa lihat semua, yang lain hanya SKPD-nya
            var canViewAll = user.IsSuperAdmin();
            var effectiveSkpdId = skpdId;
            if (!canViewAll)
            {
                var userSkpdId = user.GetSkpdId();
                if (userSkpdId.HasValue)
                    effectiveSkpdId = userSkpdId.Value;
            }

            var queryParams = new CategoryQueryParams
            {
                SkpdId = effectiveSkpdId,
                Page = page,
                PageSize = pageSize
            };

            var items = await service.GetAllAsync(queryParams, cancellationToken);
            return Results.Ok(items);
        })
        .RequireAuthorization("CanViewCategory");

        // GET category by id
        group.MapGet("/{id:int}", async (
            int id,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        // GET categories by skpd_id (untuk public, tanpa auth)
        group.MapGet("/skpd/{skpdId:int}", async (
            int skpdId,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            var items = await service.GetBySkpdIdAsync(skpdId, cancellationToken);
            return Results.Ok(items);
        })
        .RequireRateLimiting("PublicPolicy")
        .AllowAnonymous();

        // POST create category — SuperAdmin: bebas pilih SKPD, Admin/Editor: hanya SKPD-nya
        group.MapPost("/", async (
            [FromBody] CreateCategoryRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ICategoryService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            var canSelectSkpd = user.IsSuperAdmin();

            if (!canSelectSkpd)
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                request.SkpdId = skpdId.Value;
            }

            var created = await service.CreateAsync(request, cancellationToken);
            await auditService.LogAsync(user, httpContext,
                "CREATE_CATEGORY", "category.create", "category", created.Id,
                "success", "created", oldData: null, newData: created, cancellationToken);
            return Results.Created($"/api/v1/categories/{created.Id}", created);
        }).RequireAuthorization("CanCreateCategory");

        // PUT update category — SuperAdmin: bebas, Admin/Editor: hanya kategori di SKPD-nya
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateCategoryRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ICategoryService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            // Fetch existing untuk audit (sekaligus dipakai untuk SKPD scope check)
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound();

            var canEditAny = user.IsSuperAdmin();

            if (!canEditAny)
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                if (existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            var updated = await service.UpdateAsync(id, request, cancellationToken);
            if (!updated) return Results.NotFound();

            await auditService.LogAsync(user, httpContext,
                "UPDATE_CATEGORY", "category.update", "category", id,
                "success", "updated", oldData: existing, newData: request, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization("CanEditCategory");

        // DELETE category — SuperAdmin: bebas, Admin: hanya kategori di SKPD-nya
        group.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ICategoryService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            // Fetch existing untuk audit (sekaligus dipakai untuk SKPD scope check)
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound();

            var canDeleteAny = user.IsSuperAdmin();

            if (!canDeleteAny)
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                if (existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            var deleted = await service.DeleteAsync(id, cancellationToken);
            if (!deleted) return Results.NotFound();

            await auditService.LogAsync(user, httpContext,
                "DELETE_CATEGORY", "category.delete", "category", id,
                "success", "deleted", oldData: existing, newData: null, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization("CanDeleteCategory");

        return app;
    }
}