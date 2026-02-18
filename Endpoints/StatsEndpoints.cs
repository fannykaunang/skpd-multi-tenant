using System.Security.Claims;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/stats")
            .WithTags("Stats")
            .RequireAuthorization();

        // GET /api/v1/stats — statistik ringkasan dashboard
        // SuperAdmin: global, non-SuperAdmin: hanya SKPD mereka
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IStatsService service,
            CancellationToken ct) =>
        {
            int? skpdId = user.IsSuperAdmin() ? null : user.GetSkpdId();
            var stats = await service.GetStatsAsync(skpdId, ct);
            return Results.Ok(stats);
        });

        return app;
    }
}
