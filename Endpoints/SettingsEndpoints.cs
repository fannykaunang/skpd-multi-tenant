using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings")
            .WithTags("Settings")
            .RequireAuthorization();

        // GET — semua user terautentikasi bisa baca
        group.MapGet("/", async (ISettingsService service, CancellationToken ct) =>
        {
            var settings = await service.GetAsync(ct);
            if (settings is null)
                return Results.NotFound(new { message = "Pengaturan tidak ditemukan." });
            return Results.Ok(settings);
        });

        // PUT — hanya SuperAdmin (ManageAll)
        group.MapPut("/", async (
            [FromBody] UpdateSettingsRequest request,
            ClaimsPrincipal user,
            ISettingsService service,
            IAuditService auditService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.NamaAplikasi))
                return Results.BadRequest(new { message = "Nama aplikasi wajib diisi." });
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { message = "Email wajib diisi." });
            if (string.IsNullOrWhiteSpace(request.Alamat))
                return Results.BadRequest(new { message = "Alamat wajib diisi." });

            var oldSettings = await service.GetAsync(ct);

            var updatedBy = user.GetUsername() ?? user.FindFirst("sub")?.Value ?? "unknown";
            await service.UpdateAsync(request, updatedBy, ct);

            await auditService.LogAsync(user, httpContext,
                "UPDATE_SETTINGS", "settings.update", "settings", 1,
                "success", "updated", oldData: oldSettings, newData: request, ct: ct);

            return Results.Ok(new { message = "Pengaturan berhasil disimpan." });
        }).RequireAuthorization("CanManageSettings");

        return app;
    }
}
