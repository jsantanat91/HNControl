using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HNControl.Web.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IConfiguration config)
    {
        _basePath = config["Storage:BasePath"] ?? "App_Data/uploads";
    }

    public async Task<(string storagePath, long sizeBytes)> SavePdfAsync(IFormFile file, string subFolder, string fileNameNoExt)
    {
        var res = await SaveFileAsync(file, subFolder, fileNameNoExt, new[] { ".pdf" }, 15 * 1024 * 1024);
        return (res.storagePath, res.sizeBytes);
    }

    public async Task<(string storagePath, long sizeBytes, string contentType, string originalName)> SaveFileAsync(
        IFormFile file,
        string subFolder,
        string fileNameNoExt,
        string[] allowedExtensions,
        long maxBytes)
    {
        if (file == null || file.Length <= 0)
            throw new InvalidOperationException("Archivo vacío.");

        if (file.Length > maxBytes)
            throw new InvalidOperationException($"Archivo demasiado grande (max {maxBytes / (1024 * 1024)}MB).");

        // Normaliza lista permitida (case-insensitive y con punto)
        var allowed = new HashSet<string>(
            allowedExtensions.Select(e =>
            {
                var x = (e ?? "").Trim().ToLowerInvariant();
                if (!x.StartsWith(".")) x = "." + x;
                return x;
            }),
            StringComparer.OrdinalIgnoreCase
        );

        var ext = (Path.GetExtension(file.FileName) ?? "").Trim().ToLowerInvariant();

        // Si viene sin extensión, intentamos derivarla por ContentType
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = (file.ContentType ?? "").ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/heic" => ".heic",
                "application/pdf" => ".pdf",
                _ => ""
            };
        }

        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            throw new InvalidOperationException($"Tipo de archivo no permitido ({ext}).");

        var safeName = fileNameNoExt + ext;

        var folder = Path.Combine(_basePath, subFolder);
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, safeName);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(fs);

        var storagePath = Path.Combine(subFolder, safeName).Replace("\\", "/");
        return (storagePath, file.Length, file.ContentType ?? GuessContentType(fullPath), Path.GetFileName(file.FileName));
    }

    public async Task<(string storagePath, long sizeBytes, string contentType)> SaveBytesAsync(
        byte[] data,
        string subFolder,
        string fileNameWithExt,
        string contentType)
    {
        if (data == null || data.Length == 0)
            throw new InvalidOperationException("Contenido vacío.");

        var folder = Path.Combine(_basePath, subFolder);
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, fileNameWithExt);

        await File.WriteAllBytesAsync(fullPath, data);

        var storagePath = Path.Combine(subFolder, fileNameWithExt).Replace("\\", "/");
        return (storagePath, data.Length, contentType);
    }

    public Task<(Stream stream, string contentType, string downloadName)> OpenAsync(string storagePath, string downloadName)
    {
        var fullPath = Path.Combine(_basePath, storagePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No existe el archivo.");

        Stream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult((fs, GuessContentType(fullPath), downloadName));
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" or ".jfif" => "image/jpeg",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            _ => "application/octet-stream"
        };
    }
}
