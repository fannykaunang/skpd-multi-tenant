using System.Security.Claims;
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
