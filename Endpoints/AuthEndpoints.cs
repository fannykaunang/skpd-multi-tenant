using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", async (
                [FromBody] LoginRequest request,
                IAuthService authService,
                ITenantResolver tenantResolver,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var tenantId = await tenantResolver.ResolveSkpdIdAsync(context, cancellationToken);

                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = context.Request.Headers.UserAgent.ToString();

                var result = await authService.LoginAsync(
             request,
             tenantId,
             ipAddress,
             userAgent,
             cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Response);
                }

                if (result.IsLocked)
                {
                    return Results.Json(
     new
     {
         code = "account_locked",
         message = "Akun Anda terkunci sementara. Silakan coba lagi nanti."
     },
     statusCode: StatusCodes.Status423Locked);
                }

                return Results.Json(
    new
    {
        code = "invalid_credentials",
        message = "Username atau password salah."
    },
    statusCode: StatusCodes.Status401Unauthorized);


            })
            .WithName("Login")
            .AllowAnonymous();

        return app;
    }
}
