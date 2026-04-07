namespace HNControl.Web.Services;

public interface IOfficePdfConverter
{
    Task<byte[]?> TryConvertDocxToPdfAsync(byte[] docxBytes, string baseFileName, CancellationToken cancellationToken = default);
}
