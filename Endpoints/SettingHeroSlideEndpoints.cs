using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SettingHeroSlideEndpoints
{
    public static IEndpointRouteBuilder MapSettingHeroSlideEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Public: hanya GET (untuk ditampilkan di halaman utama) ───────────
        var publicGroup = app.MapGroup("/api/v1/setting-hero-slides")
            .WithTags("Setting Hero Slides")
            .RequireRateLimiting("PublicPolicy");

        publicGroup.MapGet("/", async (ISettingHeroSlideService service, CancellationToken ct) =>
        {
            var slides = await service.GetAllAsync(ct);
            return Results.Ok(new { status = "success", data = slides });
        });

        // ── Admin: CRUD (hanya SuperAdmin / ManageSettings) ──────────────────
        var adminGroup = app.MapGroup("/api/v1/setting-hero-slides")
            .WithTags("Setting Hero Slides")
            .RequireAuthorization("CanManageSettings");

        adminGroup.MapPost("/", async (
            [FromBody] UpsertSettingHeroSlideRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISettingHeroSlideService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ImageUrl))
                return Results.BadRequest(new { message = "imageUrl wajib diisi." });

            if (!IsValidTextAlign(request.TextAlign))
                return Results.BadRequest(new { message = "textAlign tidak valid." });

            if (request.OverlayOpacity < 0m || request.OverlayOpacity > 1m)
                return Results.BadRequest(new { message = "overlayOpacity harus di antara 0.00 dan 1.00." });

            request.TextAlign = request.TextAlign.Trim().ToLowerInvariant();

            var slide = await service.CreateAsync(request, ct);

            await auditService.LogAsync(user, httpContext,
                "CREATE_SETTING_HERO_SLIDE", "setting.hero_slide.create",
                "setting_hero_slides", slide.Id,
                "success", "created", newData: slide, ct: ct);

            return Results.Ok(new
            {
                status = "success",
                message = "Slide berhasil ditambahkan.",
                data = slide
            });
        });

        adminGroup.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpsertSettingHeroSlideRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISettingHeroSlideService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ImageUrl))
                return Results.BadRequest(new { message = "imageUrl wajib diisi." });

            if (!IsValidTextAlign(request.TextAlign))
                return Results.BadRequest(new { message = "textAlign tidak valid." });

            if (request.OverlayOpacity < 0m || request.OverlayOpacity > 1m)
                return Results.BadRequest(new { message = "overlayOpacity harus di antara 0.00 dan 1.00." });

            request.TextAlign = request.TextAlign.Trim().ToLowerInvariant();

            var old = await service.GetByIdAsync(id, ct);
            if (old is null) return Results.NotFound();

            var updated = await service.UpdateAsync(id, request, ct);

            await auditService.LogAsync(user, httpContext,
                "UPDATE_SETTING_HERO_SLIDE", "setting.hero_slide.update",
                "setting_hero_slides", id,
                "success", "updated", oldData: old, newData: updated, ct: ct);

            return Results.Ok(new
            {
                status = "success",
                message = "Slide berhasil diperbarui.",
                data = updated
            });
        });

        adminGroup.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISettingHeroSlideService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var old = await service.GetByIdAsync(id, ct);
            if (old is null) return Results.NotFound();

            await service.DeleteAsync(id, ct);

            await auditService.LogAsync(user, httpContext,
                "DELETE_SETTING_HERO_SLIDE", "setting.hero_slide.delete",
                "setting_hero_slides", id,
                "success", "deleted", oldData: old, ct: ct);

            return Results.NoContent();
        });

        adminGroup.MapPut("/reorder", async (
            [FromBody] ReorderSettingHeroSlidesRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISettingHeroSlideService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            await service.ReorderAsync(request.Ids, ct);

            await auditService.LogAsync(user, httpContext,
                "REORDER_SETTING_HERO_SLIDES", "setting.hero_slide.reorder",
                "setting_hero_slides", 0,
                "success", "reordered", ct: ct);

            return Results.NoContent();
        });

        return app;
    }

    private static bool IsValidTextAlign(string? value) =>
        value?.Trim().ToLowerInvariant() is
            "middle-left" or "middle-center" or "middle-right" or
            "bottom-left" or "bottom-center" or "bottom-right";
}
