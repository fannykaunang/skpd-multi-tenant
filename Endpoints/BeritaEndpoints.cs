using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class BeritaEndpoints
{
    public static IEndpointRouteBuilder MapBeritaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/berita")
            .WithTags("Berita")
            .RequireAuthorization();

        // GET all berita dengan optional filters
        group.MapGet("/", async (
            int? skpdId,
            int? categoryId,
            string? status,
            int page,
            int pageSize,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            // Set default values
            page = page > 0 ? page : 1;
            pageSize = pageSize > 0 ? pageSize : 10;

            var queryParams = new BeritaQueryParams
            {
                SkpdId = skpdId,
                CategoryId = categoryId,
                Status = status,
                Page = page,
                PageSize = pageSize
            };

            var items = await service.GetAllAsync(queryParams, cancellationToken);
            return Results.Ok(items);
        })
        .RequireRateLimiting("BeritaPolicy")
        .AllowAnonymous(); // Terapkan rate limit untuk endpoint ini

        // GET berita by id
        group.MapGet("/{id:long}", async (
            long id,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .RequireRateLimiting("BeritaPolicy")
        .AllowAnonymous(); // Terapkan rate limit untuk endpoint ini

        // GET berita by category slug (untuk public)
        group.MapGet("/category/{skpdId:int}/{categorySlug}", async (
            int skpdId,
            string categorySlug,
            int? page,
            int? pageSize,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            // Set default values
            var pageNum = page ?? 1;
            var pageSizeNum = pageSize ?? 10;

            // Ensure positive values
            pageNum = pageNum > 0 ? pageNum : 1;
            pageSizeNum = pageSizeNum > 0 ? pageSizeNum : 10;

            var items = await service.GetByCategorySlugAsync(skpdId, categorySlug, pageNum, pageSizeNum, cancellationToken);
            return Results.Ok(items);
        })
        .RequireRateLimiting("BeritaPolicy")
        .AllowAnonymous();

        // POST create berita - memerlukan authentication untuk mendapatkan userId
        group.MapPost("/", async (
            [FromBody] CreateBeritaRequest request,
            ClaimsPrincipal user,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            // Ambil user ID dari JWT claims
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst("sub")?.Value
                           ?? user.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var created = await service.CreateAsync(request, userId, cancellationToken);
                return Results.Created($"/api/v1/berita/{created.Id}", new
                {
                    status = "success",
                    message = "Berita berhasil dibuat",
                    data = new { id = created.Id } // ID dikirim untuk kebutuhan redirect di frontend
                });

                //return Results.Created($"/api/v1/berita/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "Validation Error", message = ex.Message });
            }
        });

        // PUT update berita - hanya Admin atau pemilik berita yang bisa update
        group.MapPut("/{id:long}", async (
            long id,
            [FromBody] UpdateBeritaRequest request,
            ClaimsPrincipal user,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            // Check authorization: Admin bisa update semua, user biasa hanya bisa update miliknya
            var isAdmin = user.IsInRole("Admin");

            if (!isAdmin)
            {
                var berita = await service.GetByIdAsync(id, cancellationToken);
                if (berita == null) return Results.NotFound();

                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? user.FindFirst("sub")?.Value
                               ?? user.FindFirst("userId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                if (berita.CreatedBy != userId)
                {
                    return Results.Forbid();
                }
            }

            try
            {
                var updated = await service.UpdateAsync(id, request, cancellationToken);

                if (!updated)
                {
                    return Results.NotFound(new { status = "error", message = "Gagal memperbarui berita" });
                }

                // 4. Kembalikan Pesan Sukses dan Status
                return Results.Ok(new
                {
                    status = "success",
                    message = "Berita berhasil diperbarui",
                    data = new { id }
                });

                //return updated ? Results.NoContent() : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "Validation Error", message = ex.Message });
            }
        });

        // DELETE berita - hanya Admin yang bisa delete
        group.MapDelete("/{id:long}", async (
            long id,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                return Results.NotFound(new { status = "error", message = "Gagal menghapus berita" });
            }

            // 4. Kembalikan Pesan Sukses dan Status
            return Results.Ok(new
            {
                status = "success",
                message = "Berita berhasil dihapus",
                data = new { id }
            });

            //return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        // POST increment view count - endpoint publik untuk tracking views
        group.MapPost("/{id:long}/view", async (
            long id,
            IBeritaService service,
            CancellationToken cancellationToken) =>
        {
            var success = await service.IncrementViewCountAsync(id, cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        }).AllowAnonymous();

        return app;
    }
}