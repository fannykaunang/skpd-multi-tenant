using System.Security.Claims;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit-logs")
            .WithTags("Audit Logs")
            .RequireAuthorization("CanViewAuditLogs");

        // GET /api/v1/audit-logs — daftar audit log dengan pagination + filter
        // SuperAdmin: semua log, user dengan view_audit_logs: hanya log SKPD mereka
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IAuditLogService service,
            string? search,
            string? action,
            string? entityType,
            string? status,
            int page,
            int pageSize,
            CancellationToken ct) =>
        {
            int? skpdId = user.IsSuperAdmin() ? null : user.GetSkpdId();
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : pageSize;

            var result = await service.GetListAsync(skpdId, search, action, entityType, status, page, pageSize, ct);
            return Results.Ok(result);
        });

        return app;
    }
}
