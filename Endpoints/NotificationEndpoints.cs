using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        // GET /api/v1/notifications — list latest 50 notifications for current user
        group.MapGet("/", async (
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            var items = await service.GetByUserIdAsync(userId.Value, 50, cancellationToken);
            return Results.Ok(items);
        });

        // GET /api/v1/notifications/unread-count
        group.MapGet("/unread-count", async (
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            var count = await service.GetUnreadCountAsync(userId.Value, cancellationToken);
            return Results.Ok(new UnreadCountResponse { Count = count });
        });

        // GET /api/v1/notifications/stats
        group.MapGet("/stats", async (
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            var stats = await service.GetStatsAsync(userId.Value, ct);
            return Results.Ok(stats);
        });

        // PUT /api/v1/notifications/{id}/read
        group.MapPut("/{id:long}/read", async (
            long id,
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            var updated = await service.MarkAsReadAsync(id, userId.Value, cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        // PUT /api/v1/notifications/read-all
        group.MapPut("/read-all", async (
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            await service.MarkAllAsReadAsync(userId.Value, cancellationToken);
            return Results.NoContent();
        });

        // GET /api/v1/notifications/list — paginated, searchable, filterable
        group.MapGet("/list", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? type,
            [FromQuery] string? isRead,
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var result = await service.GetPaginatedAsync(userId.Value, page, pageSize, search, type, isRead, ct);
            return Results.Ok(result);
        });

        // DELETE /api/v1/notifications/{id}
        group.MapDelete("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            var ok = await service.DeleteAsync(id, userId.Value, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        // DELETE /api/v1/notifications/batch — body: { ids: [1,2,3] }
        group.MapDelete("/batch", async (
            [FromBody] DeleteBatchRequest body,
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            var deleted = await service.DeleteBatchAsync(body.Ids, userId.Value, ct);
            return Results.Ok(new { deleted });
        });

        // DELETE /api/v1/notifications/clear — delete all for current user
        group.MapDelete("/clear", async (
            ClaimsPrincipal user,
            INotificationService service,
            CancellationToken ct) =>
        {
            var userId = ResolveUserId(user);
            if (userId is null) return Results.Unauthorized();

            await service.DeleteAllAsync(userId.Value, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static long? ResolveUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? user.FindFirst("sub")?.Value
                 ?? user.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(claim) || !long.TryParse(claim, out var id))
            return null;
        return id;
    }
}
