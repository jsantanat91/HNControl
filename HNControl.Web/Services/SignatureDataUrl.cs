namespace HNControl.Web.Services;

public static class SignatureDataUrl
{
    public static byte[]? ParsePng(string? dataUrl)
    {
        if (dataUrl == null || !dataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal)
            || dataUrl.Length > 2_800_000) return null;
        try
        {
            var bytes = Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',') + 1)..]);
            return bytes.Length is >= 200 and <= 2_000_000
                && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? bytes : null;
        }
        catch (FormatException) { return null; }
    }
}
