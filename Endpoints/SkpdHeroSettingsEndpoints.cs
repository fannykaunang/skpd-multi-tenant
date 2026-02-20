using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SkpdHeroSettingsEndpoints
{
    public static IEndpointRouteBuilder MapSkpdHeroSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/skpd-hero-settings")
            .WithTags("SKPD Hero Settings")
            .RequireAuthorization("CanManageSkpdSections");

        group.MapGet("/", async (
            int? skpdId,
            ClaimsPrincipal user,
            ISkpdHeroSettingsService service,
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
                return Results.Ok(new SkpdHeroSettings
                {
                    Id = 0,
                    SkpdId = effectiveSkpdId.Value,
                    HeroType = "image",
                    Title = null,
                    Subtitle = null,
                    BackgroundImage = null,
                    OverlayOpacity = 0.50m
                });
            }

            return Results.Ok(item);
        });

        group.MapPut("/", async (
            [FromBody] UpsertSkpdHeroSettingsRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ISkpdHeroSettingsService service,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            if (user.IsSuperAdmin() && !request.SkpdId.HasValue)
                return Results.BadRequest(new { message = "SuperAdmin wajib mengirim skpdId." });

            var effectiveSkpdId = ResolveEffectiveSkpdId(user, request.SkpdId);
            if (!effectiveSkpdId.HasValue)
                return Results.Forbid();

            var heroType = (request.HeroType ?? string.Empty).Trim().ToLowerInvariant();
            if (heroType is not ("image" or "slider" or "video"))
                return Results.BadRequest(new { message = "heroType harus bernilai: image, slider, atau video." });

            if (request.OverlayOpacity < 0m || request.OverlayOpacity > 1m)
                return Results.BadRequest(new { message = "overlayOpacity harus di antara 0.00 sampai 1.00." });

            request.HeroType = heroType;

            var oldData = await service.GetBySkpdIdAsync(effectiveSkpdId.Value, ct);
            var updated = await service.UpsertAsync(effectiveSkpdId.Value, request, ct);

            await auditService.LogAsync(user, httpContext,
                "UPSERT_SKPD_HERO_SETTINGS", "skpd.hero_settings.upsert", "skpd_hero_settings", updated.Id,
                "success", "updated", oldData: oldData, newData: updated, ct: ct);

            return Results.Ok(new
            {
                status = "success",
                message = "Pengaturan hero SKPD berhasil disimpan.",
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
