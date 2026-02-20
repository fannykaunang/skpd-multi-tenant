using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Extensions;
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
        .RequireRateLimiting("PublicPolicy")
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
        .RequireRateLimiting("PublicPolicy")
        .AllowAnonymous();

        // POST create berita - SuperAdmin/Admin: bebas, Operator SKPD: hanya di SKPD-nya + status draft/review
        group.MapPost("/", async (
            [FromBody] CreateBeritaRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IBeritaService service,
            ITagService tagService,
            IAuditService auditService,
            INotificationService notificationService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("BeritaEndpoints");
            // Ambil user ID dari JWT claims
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst("sub")?.Value
                           ?? user.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            // Hanya SuperAdmin dan Admin yang boleh memilih SKPD bebas.
            // Role lain (Editor/Operator) wajib menggunakan SKPD dari token.
            var canSelectSkpd = user.IsSuperAdmin() || user.IsAdmin();

            if (!canSelectSkpd)
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue)
                    return Results.Forbid();

                request.SkpdId = skpdId.Value;
            }

            // Operator tanpa permission publish hanya bisa set draft/review
            if (!user.HasPermission("publish_berita") && !user.HasPermission("manage_all"))
            {
                var allowed = new[] { "draft", "review" };
                if (!allowed.Contains(request.Status?.ToLower() ?? "draft"))
                {
                    request.Status = "draft";
                }
            }

            try
            {
                var created = await service.CreateAsync(request, userId, cancellationToken);
                await tagService.SetBeritaTagsAsync(created.Id, request.TagIds ?? [], cancellationToken);

                try
                {
                    var targets = await notificationService.GetUsersToNotifyAsync(
                        created.SkpdId, userId, cancellationToken);
                    logger.LogInformation(
                        "Berita {BeritaId} (skpd={SkpdId}): sending notifications to {Count} user(s).",
                        created.Id, created.SkpdId, targets.Count);
                    var creatorName = user.FindFirst(ClaimTypes.Name)?.Value
                                   ?? user.FindFirst("unique_name")?.Value ?? "Operator";
                    foreach (var targetId in targets)
                        await notificationService.CreateAsync(
                            targetId.ToString(),
                            "Berita Baru Menunggu Tinjauan",
                            $"\"{created.Title}\" telah dibuat oleh {creatorName}.",
                            $"/dashboard/berita/edit/{created.Id}",
                            "info", cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Notification dispatch failed for berita {BeritaId} — berita creation is unaffected.",
                        created.Id);
                }

                await auditService.LogAsync(user, httpContext,
                    "CREATE_BERITA", "berita.create", "berita", created.Id,
                    "success", "created", oldData: null, newData: created, cancellationToken);
                return Results.Created($"/api/v1/berita/{created.Id}", new
                {
                    status = "success",
                    message = "Berita berhasil dibuat",
                    data = new { id = created.Id }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "Validation Error", message = ex.Message });
            }
        })
        .RequireAuthorization("CanCreateBerita");

        // PUT update berita - SuperAdmin: bebas, Operator SKPD: hanya berita di SKPD-nya atau miliknya
        group.MapPut("/{id:long}", async (
            long id,
            [FromBody] UpdateBeritaRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IBeritaService service,
            ITagService tagService,
            IAuditService auditService,
            INotificationService notificationService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var putLogger = loggerFactory.CreateLogger("BeritaEndpoints");

            // Ambil userId untuk notifikasi
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst("sub")?.Value
                           ?? user.FindFirst("userId")?.Value;
            long.TryParse(userIdClaim, out var currentUserId);

            // Fetch existing untuk audit (sekaligus dipakai untuk SKPD scope check)
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Berita tidak ditemukan" });

            // Gate: hanya yang punya edit_berita (atau manage_all) yang boleh edit
            if (!user.HasPermission("edit_berita"))
                return Results.Forbid();

            // Scope: SuperAdmin & Admin bisa edit berita mana saja
            // Editor SKPD: hanya berita di SKPD-nya sendiri
            if (!user.IsSuperAdmin() && !user.IsAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            // Operator tanpa permission publish hanya bisa set draft/review
            if (!user.HasPermission("publish_berita") && !user.HasPermission("manage_all"))
            {
                var allowed = new[] { "draft", "review" };
                if (!allowed.Contains(request.Status?.ToLower() ?? "draft"))
                {
                    request.Status = "draft";
                }
            }

            try
            {
                var updated = await service.UpdateAsync(id, request, cancellationToken);

                if (!updated)
                {
                    return Results.NotFound(new { status = "error", message = "Gagal memperbarui berita" });
                }

                if (request.TagIds != null)
                    await tagService.SetBeritaTagsAsync(id, request.TagIds, cancellationToken);

                try
                {
                    var editorName = user.FindFirst(ClaimTypes.Name)?.Value
                                  ?? user.FindFirst("unique_name")?.Value ?? "Editor";
                    var isPublishTransition =
                        string.Equals(request.Status, "published", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(existing.Status, "published", StringComparison.OrdinalIgnoreCase);

                    if (isPublishTransition)
                    {
                        // Notifikasi ke operator pembuat berita
                        if (existing.CreatedBy.HasValue && existing.CreatedBy.Value != currentUserId)
                            await notificationService.CreateAsync(
                                existing.CreatedBy.Value.ToString(),
                                "Berita Anda Telah Diterbitkan",
                                $"\"{existing.Title}\" telah disetujui dan diterbitkan oleh {editorName}.",
                                $"/dashboard/berita/edit/{id}",
                                "success", cancellationToken);

                        // Notifikasi ke admin SKPD (manage_all + skpd_id = skpdId → bukan SuperAdmin)
                        var admins = await notificationService.GetSkpdAdminsAsync(
                            existing.SkpdId, currentUserId, cancellationToken);
                        putLogger.LogInformation(
                            "Berita {BeritaId} published: notifying creator={CreatorId}, {AdminCount} SKPD admin(s).",
                            id, existing.CreatedBy, admins.Count);
                        foreach (var adminId in admins)
                            await notificationService.CreateAsync(
                                adminId.ToString(),
                                "Berita Diterbitkan",
                                $"\"{existing.Title}\" telah diterbitkan oleh {editorName}.",
                                $"/dashboard/berita/edit/{id}",
                                "success", cancellationToken);
                    }
                    else
                    {
                        // Update biasa → notifikasi ke editor SKPD (edit_berita, skpd sama, bukan pelaku)
                        var editors = await notificationService.GetEditorsBySkpdAsync(
                            existing.SkpdId, currentUserId, cancellationToken);
                        putLogger.LogInformation(
                            "Berita {BeritaId} updated: notifying {EditorCount} editor(s).",
                            id, editors.Count);
                        foreach (var editorId in editors)
                            await notificationService.CreateAsync(
                                editorId.ToString(),
                                "Berita Diperbarui",
                                $"\"{existing.Title}\" telah diperbarui oleh {editorName}.",
                                $"/dashboard/berita/edit/{id}",
                                "info", cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    putLogger.LogWarning(ex,
                        "Notification dispatch failed for berita update {BeritaId} — update is unaffected.", id);
                }

                await auditService.LogAsync(user, httpContext,
                    "UPDATE_BERITA", "berita.update", "berita", id,
                    "success", "updated", oldData: existing, newData: request, cancellationToken);
                return Results.Ok(new
                {
                    status = "success",
                    message = "Berita berhasil diperbarui",
                    data = new { id }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "Validation Error", message = ex.Message });
            }
        })
        .RequireAuthorization();

        // DELETE berita - SuperAdmin: bebas, Operator SKPD: hanya berita di SKPD-nya
        group.MapDelete("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IBeritaService service,
            IAuditService auditService,
            CancellationToken cancellationToken) =>
        {
            // Fetch existing untuk audit (sekaligus dipakai untuk SKPD scope check)
            var existing = await service.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Berita tidak ditemukan" });

            // Gate: hanya yang punya delete_berita (atau manage_all) yang boleh hapus
            if (!user.HasPermission("delete_berita"))
                return Results.Forbid();

            // Scope: SuperAdmin & Admin bisa hapus berita mana saja
            // yang lain: hanya berita di SKPD-nya sendiri
            if (!user.IsSuperAdmin() && !user.IsAdmin())
            {
                var skpdId = user.GetSkpdId();
                if (!skpdId.HasValue || existing.SkpdId != skpdId.Value)
                    return Results.Forbid();
            }

            var deleted = await service.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                return Results.NotFound(new { status = "error", message = "Gagal menghapus berita" });
            }

            await auditService.LogAsync(user, httpContext,
                "DELETE_BERITA", "berita.delete", "berita", id,
                "success", "deleted", oldData: existing, newData: null, cancellationToken);
            return Results.Ok(new
            {
                status = "success",
                message = "Berita berhasil dihapus",
                data = new { id }
            });
        }).RequireAuthorization();

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
