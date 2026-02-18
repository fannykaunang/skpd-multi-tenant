namespace skpd_multi_tenant_api.Endpoints;

public static class UploadEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/upload")
            .WithTags("Upload")
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapPost("/image", async (IFormFile file, IWebHostEnvironment env) =>
        {
            if (file.Length == 0)
                return Results.BadRequest(new { message = "File tidak boleh kosong." });

            if (file.Length > MaxFileSize)
                return Results.BadRequest(new { message = "Ukuran file maksimal 5 MB." });

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
                return Results.BadRequest(new { message = $"Format file '{extension}' tidak diizinkan. Gunakan: jpg, jpeg, png, gif, webp, svg." });

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return Results.BadRequest(new { message = $"Tipe file '{file.ContentType}' tidak diizinkan." });

            // Build path: wwwroot/uploads/YYYY/MM/
            var now = DateTime.UtcNow;
            var relativePath = Path.Combine("uploads", now.ToString("yyyy"), now.ToString("MM"));
            var uploadDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), relativePath);
            Directory.CreateDirectory(uploadDir);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/uploads/{now:yyyy}/{now:MM}/{fileName}";

            return Results.Ok(new
            {
                status = "success",
                message = "File berhasil diupload",
                data = new { url, fileName, size = file.Length }
            });
        });

        return app;
    }
}
