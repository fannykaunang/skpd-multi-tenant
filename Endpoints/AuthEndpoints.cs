using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Extensions;
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
                    request, tenantId, ipAddress, userAgent, cancellationToken);

                if (result.RequiresOtp)
                {
                    return Results.Ok(new
                    {
                        requiresOtp = true,
                        email = MaskEmail(result.Email!),
                        message = "Kode OTP telah dikirim ke email Anda."
                    });
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

        group.MapPost("/verify-otp", async (
                [FromBody] VerifyOtpRequest request,
                IAuthService authService,
                ITenantResolver tenantResolver,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var tenantId = await tenantResolver.ResolveSkpdIdAsync(context, cancellationToken);
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = context.Request.Headers.UserAgent.ToString();

                var result = await authService.VerifyOtpAndLoginAsync(
                    request, tenantId, ipAddress, userAgent, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Response!;

                    SetTokenCookies(context, response);

                    return Results.Ok(new
                    {
                        status = "success",
                        message = "Login berhasil",
                        data = new
                        {
                            username = response.Username,
                            skpdId = response.SkpdId,
                            expiresAtUtc = response.ExpiresAtUtc
                        }
                    });
                }

                return Results.Json(
                    new
                    {
                        code = "invalid_otp",
                        message = "Kode OTP tidak valid atau sudah kedaluwarsa."
                    },
                    statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithName("VerifyOtp")
            .AllowAnonymous();

        group.MapPost("/refresh", async (
                IAuthService authService,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var refreshToken = context.Request.Cookies["refreshToken"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Results.Json(
                        new { code = "no_refresh_token", message = "Refresh token tidak ditemukan." },
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var response = await authService.RefreshTokenAsync(refreshToken, cancellationToken);

                if (response is null)
                {
                    // Clear invalid cookies
                    ClearTokenCookies(context);

                    return Results.Json(
                        new
                        {
                            code = "invalid_refresh_token",
                            message = "Refresh token tidak valid atau sudah kedaluwarsa."
                        },
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                SetTokenCookies(context, response);

                return Results.Ok(new
                {
                    status = "success",
                    message = "Token berhasil diperbarui"
                });
            })
            .WithName("RefreshToken")
            .AllowAnonymous();

        // GET /auth/profile — data lengkap pengguna yang sedang login
        group.MapGet("/profile", async (
                ClaimsPrincipal user,
                IPenggunaService penggunaService,
                CancellationToken cancellationToken) =>
            {
                var userId = user.GetUserId();
                if (!userId.HasValue)
                    return Results.Unauthorized();

                var profile = await penggunaService.GetByIdAsync(userId.Value, cancellationToken);
                if (profile is null)
                    return Results.NotFound(new { message = "Profil tidak ditemukan." });

                return Results.Ok(profile);
            })
            .WithName("GetProfile")
            .RequireAuthorization();

        // PUT /auth/profile/password — ganti password
        group.MapPut("/profile/password", async (
                ChangePasswordRequest request,
                ClaimsPrincipal user,
                HttpContext httpContext,
                IPenggunaService penggunaService,
                IAuditService auditService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                    return Results.BadRequest(new { message = "Password lama dan baru wajib diisi." });

                if (request.NewPassword.Length < 8)
                    return Results.BadRequest(new { message = "Password baru minimal 8 karakter." });

                var userId = user.GetUserId();
                if (!userId.HasValue)
                    return Results.Unauthorized();

                var success = await penggunaService.ChangePasswordAsync(
                    userId.Value, request.OldPassword, request.NewPassword, cancellationToken);

                if (!success)
                    return Results.BadRequest(new { message = "Password lama tidak sesuai." });

                await auditService.LogAsync(user, httpContext,
                    "CHANGE_PASSWORD", "auth.password_change", "user", userId.Value,
                    "success", "password_changed", ct: cancellationToken);

                return Results.Ok(new { message = "Password berhasil diubah." });
            })
            .WithName("ChangePassword")
            .RequireAuthorization();

        group.MapGet("/me", async (
                ClaimsPrincipal user,
                IPenggunaService penggunaService,
                CancellationToken cancellationToken) =>
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userId) || !long.TryParse(userId, out var userIdLong))
                    return Results.Unauthorized();

                var username = user.FindFirst(ClaimTypes.Name)?.Value
                            ?? user.FindFirst("unique_name")?.Value
                            ?? "";

                var skpdIdClaim = user.FindFirst("skpd_id")?.Value;
                int? skpdId = !string.IsNullOrEmpty(skpdIdClaim) ? int.Parse(skpdIdClaim) : null;

                // Query permissions live dari DB agar perubahan role langsung berlaku
                // tanpa operator harus logout/login ulang
                var permissions = await penggunaService.GetPermissionsAsync(userIdLong, skpdId, cancellationToken);

                return Results.Ok(new
                {
                    userId,
                    username,
                    skpdId,
                    permissions,
                    isSuperAdmin = permissions.Contains("manage_all")
                });
            })
            .WithName("GetMe")
            .RequireAuthorization();

        group.MapPost("/logout", (HttpContext context) =>
            {
                ClearTokenCookies(context);

                return Results.Ok(new
                {
                    status = "success",
                    message = "Logout berhasil"
                });
            })
            .WithName("Logout")
            .AllowAnonymous();

        return app;
    }

    private static void SetTokenCookies(HttpContext context, LoginResponse response)
    {
        var isProduction = !string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development", StringComparison.OrdinalIgnoreCase);

        context.Response.Cookies.Append("accessToken", response.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = response.ExpiresAtUtc
        });

        context.Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",  // Only sent to auth endpoints
            Expires = response.RefreshTokenExpiresAtUtc
        });
    }

    private static void ClearTokenCookies(HttpContext context)
    {
        context.Response.Cookies.Delete("accessToken", new CookieOptions { Path = "/" });
        context.Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/v1/auth" });
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
            return email;

        var local = email[..atIndex];
        var domain = email[atIndex..];
        var visible = Math.Min(3, local.Length);

        return string.Concat(local.AsSpan(0, visible), new string('*', local.Length - visible), domain);
    }
}
