using System.Security.Claims;
using System.Text.RegularExpressions;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class MediaEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf"
    };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private const int MaxFiles = 8;

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/media")
            .WithTags("Media")
            .RequireAuthorization();

        // GET /api/v1/media — SuperAdmin: all; others: SKPD-scoped
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IMediaService service,
            CancellationToken ct) =>
        {
            var skpdId = user.IsSuperAdmin() ? null : user.GetSkpdId();
            var items = await service.GetAllAsync(skpdId, ct);
            return Results.Ok(new { status = "success", data = items });
        }).RequireAuthorization("CanViewMedia");

        // POST /api/v1/media/upload — multipart form with title, description, slug, files
        group.MapPost("/upload", async (
            IFormCollection form,
            ClaimsPrincipal user,
            IWebHostEnvironment env,
            IMediaService service,
            IAuditService auditService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var files = form.Files;

            if (files.Count == 0)
                return Results.BadRequest(new { status = "error", message = "Tidak ada file yang dipilih." });

            if (files.Count > MaxFiles)
                return Results.BadRequest(new { status = "error", message = $"Maksimal {MaxFiles} file per upload." });

            // Resolve SKPD ID
            int skpdId;
            if (user.IsSuperAdmin())
            {
                var skpdIdStr = form["skpdId"].FirstOrDefault();
                if (!int.TryParse(skpdIdStr, out skpdId) || skpdId <= 0)
                    return Results.BadRequest(new { status = "error", message = "SuperAdmin harus menentukan SKPD ID tujuan upload." });
            }
            else
            {
                var userSkpdId = user.GetSkpdId();
                if (!userSkpdId.HasValue)
                    return Results.BadRequest(new { status = "error", message = "SKPD tidak ditemukan pada sesi login." });
                skpdId = userSkpdId.Value;
            }

            // Batch metadata — shared across all files in this upload
            var title = form["title"].FirstOrDefault()?.Trim() ?? string.Empty;
            var description = form["description"].FirstOrDefault()?.Trim();
            var slugInput = form["slug"].FirstOrDefault()?.Trim();

            // Determine slug: explicit input → auto from title → null
            var slug = !string.IsNullOrEmpty(slugInput) ? slugInput
                : !string.IsNullOrEmpty(title) ? GenerateSlug(title)
                : null;

            var userId = user.GetUserId();
            var now = DateTime.UtcNow;
            var relativePath = Path.Combine("uploads", now.ToString("yyyy"), now.ToString("MM"));
            var uploadDir = Path.Combine(
                env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
                relativePath);
            Directory.CreateDirectory(uploadDir);

            var results = new List<MediaItem>();
            var errors = new List<object>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                {
                    errors.Add(new { file = file.FileName, error = "File kosong." });
                    continue;
                }
                if (file.Length > MaxFileSize)
                {
                    errors.Add(new { file = file.FileName, error = "Ukuran file maksimal 5 MB." });
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                {
                    errors.Add(new { file = file.FileName, error = "Format file tidak diizinkan. Gunakan: jpg, jpeg, png, gif, webp, pdf." });
                    continue;
                }
                if (!AllowedMimeTypes.Contains(file.ContentType))
                {
                    errors.Add(new { file = file.FileName, error = $"Tipe MIME '{file.ContentType}' tidak diizinkan." });
                    continue;
                }
                if (!await ValidateMagicBytesAsync(file, extension))
                {
                    errors.Add(new { file = file.FileName, error = "Konten file tidak sesuai dengan format yang dideklarasikan." });
                    continue;
                }

                var safeFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var filePath = Path.Combine(uploadDir, safeFileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, ct);
                }

                var storedPath = $"/uploads/{now:yyyy}/{now:MM}/{safeFileName}";

                var originalName = Path.GetFileName(file.FileName);
                if (originalName.Length > 255) originalName = originalName[..255];

                var mediaItem = await service.CreateAsync(new MediaItem
                {
                    SkpdId = skpdId,
                    UploadedBy = userId,
                    FileName = originalName,
                    Title = !string.IsNullOrEmpty(title) ? title : originalName,
                    Slug = slug,
                    Description = string.IsNullOrEmpty(description) ? null : description,
                    FilePath = storedPath,
                    FileType = file.ContentType,
                    FileSize = (int)file.Length,
                }, ct);

                results.Add(mediaItem);
            }

            await auditService.LogAsync(user, httpContext,
                "UPLOAD_MEDIA", "media.upload", "media", null,
                errors.Count == 0 ? "success" : "partial", "uploaded",
                oldData: null,
                newData: new { skpdId, slug, uploadedCount = results.Count, errorCount = errors.Count },
                ct);

            return Results.Ok(new
            {
                status = errors.Count == 0 ? "success" : "partial",
                message = $"{results.Count} file berhasil diupload" + (errors.Count > 0 ? $", {errors.Count} gagal." : "."),
                data = results,
                errors
            });
        })
        .RequireAuthorization("CanUploadMedia")
        .DisableAntiforgery();

        // DELETE /api/v1/media/{id}
        group.MapDelete("/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            IWebHostEnvironment env,
            IMediaService service,
            IAuditService auditService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var existing = await service.GetByIdAsync(id, ct);
            if (existing is null)
                return Results.NotFound(new { status = "error", message = "Media tidak ditemukan." });

            if (!user.IsSuperAdmin())
            {
                var userSkpdId = user.GetSkpdId();
                if (existing.SkpdId != userSkpdId)
                    return Results.Forbid();
            }

            var deleted = await service.DeleteAsync(id, ct);
            if (!deleted)
                return Results.NotFound(new { status = "error", message = "Gagal menghapus media." });

            try
            {
                if (!string.IsNullOrEmpty(existing.FilePath))
                {
                    var wwwroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
                    var physicalPath = Path.Combine(
                        wwwroot,
                        existing.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(physicalPath))
                        File.Delete(physicalPath);
                }
            }
            catch { /* non-critical */ }

            await auditService.LogAsync(user, httpContext,
                "DELETE_MEDIA", "media.delete", "media", id,
                "success", "deleted",
                oldData: existing, newData: null, ct);

            return Results.Ok(new { status = "success", message = "Media berhasil dihapus." });
        }).RequireAuthorization("CanDeleteMedia");

        return app;
    }

    /// <summary>
    /// Converts arbitrary text to a URL-safe slug.
    /// "Foto Rapat 2026" → "foto-rapat-2026"
    /// </summary>
    private static string GenerateSlug(string text)
    {
        var slug = text.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = slug.Trim('-');
        return slug.Length > 0 ? slug : "media";
    }

    private static async Task<bool> ValidateMagicBytesAsync(IFormFile file, string extension)
    {
        var buffer = new byte[12];
        int bytesRead;
        await using (var stream = file.OpenReadStream())
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" =>
                bytesRead >= 3 &&
                buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,

            ".png" =>
                bytesRead >= 4 &&
                buffer[0] == 0x89 && buffer[1] == 0x50 &&
                buffer[2] == 0x4E && buffer[3] == 0x47,

            ".gif" =>
                bytesRead >= 6 &&
                buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 &&
                buffer[3] == 0x38 && (buffer[4] == 0x37 || buffer[4] == 0x39) &&
                buffer[5] == 0x61,

            ".webp" =>
                bytesRead >= 12 &&
                buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
                buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50,

            ".pdf" =>
                bytesRead >= 4 &&
                buffer[0] == 0x25 && buffer[1] == 0x50 &&
                buffer[2] == 0x44 && buffer[3] == 0x46,

            _ => false,
        };
    }
}
