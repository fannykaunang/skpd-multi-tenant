using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // GET all categories dengan optional filters
        group.MapGet("/", async (
            int? skpdId,
            int page,
            int pageSize,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            // Set default values
            page = page > 0 ? page : 1;
            pageSize = pageSize > 0 ? pageSize : 10;

            var queryParams = new CategoryQueryParams
            {
                SkpdId = skpdId,
                Page = page,
                PageSize = pageSize
            };

            var items = await service.GetAllAsync(queryParams, cancellationToken);
            return Results.Ok(items);
        });

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
        .RequireRateLimiting("BeritaPolicy")
        .AllowAnonymous();

        // POST create category
        group.MapPost("/", async (
            [FromBody] CreateCategoryRequest request,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/v1/categories/{created.Id}", created);
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        // PUT update category - Admin only
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateCategoryRequest request,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateAsync(id, request, cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        // DELETE category - Admin only
        group.MapDelete("/{id:int}", async (
            int id,
            ICategoryService service,
            CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        return app;
    }
}