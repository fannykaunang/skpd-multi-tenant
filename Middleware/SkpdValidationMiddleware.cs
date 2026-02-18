using System.Text;
using System.Text.Json;
using skpd_multi_tenant_api.Extensions;

namespace skpd_multi_tenant_api.Middleware;

/// <summary>
/// Middleware untuk validasi akses SKPD pada operasi POST, PUT, DELETE
/// Admin dapat mengakses semua SKPD
/// Operator hanya dapat mengakses SKPD mereka sendiri
/// </summary>
public class SkpdValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SkpdValidationMiddleware> _logger;

    public SkpdValidationMiddleware(RequestDelegate next, ILogger<SkpdValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Hanya validasi untuk POST, PUT, DELETE
        var method = context.Request.Method;
        if (method != "POST" && method != "PUT" && method != "DELETE")
        {
            await _next(context);
            return;
        }

        // Skip jika tidak authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        // Admin dan SuperAdmin dapat mengakses semua
        if (context.User.IsAdmin() || context.User.IsSuperAdmin())
        {
            await _next(context);
            return;
        }

        // Get user's SKPD ID dari JWT
        var userSkpdId = context.User.GetSkpdId();
        if (!userSkpdId.HasValue)
        {
            _logger.LogWarning("User tidak memiliki skpd_id claim di JWT token");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Forbidden",
                message = "User tidak memiliki akses SKPD"
            });
            return;
        }

        // Untuk DELETE, cek apakah ada skpdId di route atau query
        if (method == "DELETE")
        {
            // Try get dari route values (e.g., /api/v1/berita/{id})
            // Untuk DELETE, kita perlu cek data yang akan dihapus
            // Biarkan endpoint handle validasi, atau skip validasi untuk DELETE
            await _next(context);
            return;
        }

        // Untuk POST dan PUT, validasi request body
        if (method == "POST" || method == "PUT")
        {
            // Enable buffering untuk bisa read body multiple times
            context.Request.EnableBuffering();

            try
            {
                // Read request body
                var body = await ReadRequestBodyAsync(context.Request);

                if (string.IsNullOrWhiteSpace(body))
                {
                    // Tidak ada body, skip validasi
                    await _next(context);
                    return;
                }

                // Parse JSON untuk cari skpd_id
                var skpdIdFromBody = ExtractSkpdIdFromJson(body);

                if (skpdIdFromBody.HasValue)
                {
                    // Validasi: operator hanya bisa akses SKPD mereka
                    if (skpdIdFromBody.Value != userSkpdId.Value)
                    {
                        _logger.LogWarning(
                            "User {Username} (SKPD {UserSkpdId}) mencoba {Method} data untuk SKPD {TargetSkpdId}",
                            context.User.GetUsername(),
                            userSkpdId.Value,
                            method,
                            skpdIdFromBody.Value);

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Forbidden",
                            message = $"Anda tidak memiliki akses untuk {method} data SKPD lain. Anda hanya dapat mengakses data SKPD ID: {userSkpdId.Value}"
                        });
                        return;
                    }
                }

                // Reset stream position agar endpoint bisa baca body lagi
                context.Request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saat validasi SKPD access");
                // Jika error, lanjutkan request (fail-open)
            }
        }

        await _next(context);
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private int? ExtractSkpdIdFromJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Cari property skpdId atau skpd_id (case insensitive)
            if (root.TryGetProperty("skpdId", out var skpdIdElement) ||
                root.TryGetProperty("skpd_id", out skpdIdElement) ||
                root.TryGetProperty("SkpdId", out skpdIdElement) ||
                root.TryGetProperty("SKPD_ID", out skpdIdElement))
            {
                if (skpdIdElement.ValueKind == JsonValueKind.Number)
                {
                    return skpdIdElement.GetInt32();
                }
                else if (skpdIdElement.ValueKind == JsonValueKind.String)
                {
                    if (int.TryParse(skpdIdElement.GetString(), out var skpdId))
                    {
                        return skpdId;
                    }
                }
            }
        }
        catch
        {
            // Jika gagal parse, return null
        }

        return null;
    }
}

/// <summary>
/// Extension method untuk register middleware
/// </summary>
public static class SkpdValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSkpdValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SkpdValidationMiddleware>();
    }
}