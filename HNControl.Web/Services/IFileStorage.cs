using Microsoft.AspNetCore.Http;

namespace HNControl.Web.Services;

public interface IFileStorage
{
    Task<(string storagePath, long sizeBytes)> SavePdfAsync(IFormFile file, string subFolder, string fileNameNoExt);

    Task<(string storagePath, long sizeBytes, string contentType, string originalName)> SaveFileAsync(
        IFormFile file,
        string subFolder,
        string fileNameNoExt,
        string[] allowedExtensions,
        long maxBytes);

    Task<(string storagePath, long sizeBytes, string contentType)> SaveBytesAsync(
        byte[] data,
        string subFolder,
        string fileNameWithExt,
        string contentType);

    Task<(Stream stream, string contentType, string downloadName)> OpenAsync(string storagePath, string downloadName);

    /// <summary>
    /// Borra un archivo si existe. Si no existe, no lanza.
    /// </summary>
    Task DeleteIfExistsAsync(string storagePath);
}
