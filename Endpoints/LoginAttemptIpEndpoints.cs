using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints
{
    public static class LoginAttemptIpEndpoints
    {
        public static IEndpointRouteBuilder MapLoginAttemptIpEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v1/login-attempts")
                .WithTags("Security")
                .RequireAuthorization("ManageAll");

            group.MapGet("/", async (
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 10,
                [FromQuery] string? search = null,
                ILoginAttemptIpService service = null!,
                CancellationToken ct = default!) =>
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var result = await service.GetAllAsync(page, pageSize, search, ct);
                return Results.Ok(result);
            });

            group.MapDelete("/{id:long}", async (
                long id,
                ILoginAttemptIpService service,
                IAuditService auditService,
                ClaimsPrincipal user,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                var success = await service.DeleteAsync(id, ct);
                if (!success) return Results.NotFound();

                await auditService.LogAsync(user, httpContext, "DELETE_LOGIN_ATTEMPT", "security.login_attempt.delete", "login_attempts_ip", id, "success", "deleted", ct: ct);
                return Results.NoContent();
            });

            group.MapDelete("/clear", async (
                ILoginAttemptIpService service,
                IAuditService auditService,
                ClaimsPrincipal user,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                await service.ClearAllAsync(ct);
                await auditService.LogAsync(user, httpContext, "CLEAR_LOGIN_ATTEMPTS", "security.login_attempt.clear", "login_attempts_ip", null, "success", "cleared all", ct: ct);
                return Results.NoContent();
            });

            return app;
        }
    }
}
