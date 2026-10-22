using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

public class ClientDocumentModel(
    ApplicationDbContext db, IFileStorage storage, ClientDocumentSigningService signing,
    ILogger<ClientDocumentModel> logger) : PageModel
{
    public ClientLegalDocument? DocumentRef { get; set; }
    public string? Error { get; set; }
    [TempData] public string? Notice { get; set; }

    private static bool CanRead(ClientLegalDocument doc) => doc.SignedAt.HasValue
        || (doc.Status == ClientLegalDocumentStatus.SentForSignature && doc.TokenExpiresAt > DateTime.UtcNow);

    public async Task<IActionResult> OnGetAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        DocumentRef = await db.ClientLegalDocuments.Include(x => x.Client).FirstOrDefaultAsync(x => x.PublicToken == token);
        return DocumentRef == null || !CanRead(DocumentRef) ? NotFound() : Page();
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var doc = await db.ClientLegalDocuments.FirstOrDefaultAsync(x => x.PublicToken == token);
        if (doc == null || !CanRead(doc)) return NotFound();
        if (string.IsNullOrWhiteSpace(doc.PdfStoragePath)) return BadRequest("Aún no hay PDF.");
        var fileName = $"{(doc.DocumentType == ClientLegalDocumentType.NDA ? "nda" : "contrato")}_{doc.Id:N}.pdf";
        var (stream, contentType, _) = await storage.OpenAsync(doc.PdfStoragePath, fileName);
        return File(stream, contentType, fileName);
    }

    public async Task<IActionResult> OnPostSignAsync(string token, string signerName, string signerEmail, string sigDataUrl, bool acceptTerms)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var doc = await db.ClientLegalDocuments.Include(x => x.Client).Include(x => x.ClientServiceContract)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        if (doc == null || !CanRead(doc)) return NotFound();
        if (doc.SignedAt.HasValue) return RedirectToPage(new { token });
        DocumentRef = doc;
        signerName = (signerName ?? "").Trim();
        signerEmail = (signerEmail ?? "").Trim();
        if (signerName.Length is < 2 or > 200 || signerEmail.Length > 256 || !ClientCommunicationTemplates.IsEmail(signerEmail) || !acceptTerms)
        {
            Error = "Ingrese su nombre y correo válidos y confirme la aceptación del documento.";
            return Page();
        }
        var bytes = ParseDataUrl(sigDataUrl);
        if (bytes == null)
        {
            Error = "La firma es inválida. Vuelva a dibujarla.";
            return Page();
        }
        try
        {
            var (sigPath, _, _) = await storage.SaveBytesAsync(bytes, $"clients/{doc.ClientId}/legal/{doc.Id}/signatures", $"sig_{Guid.NewGuid():N}.png", "image/png");
            doc.SignatureStoragePath = sigPath;
            doc.SignedByName = signerName;
            doc.SignedByEmail = signerEmail;
            doc.SignedAt = DateTime.UtcNow;
            // Conversion and storage must succeed before persisting the signature and final status.
            await signing.SaveSignedPdfAsync(doc);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo registrar firma cliente para {DocumentId}", doc.Id);
            db.ChangeTracker.Clear();
            DocumentRef = await db.ClientLegalDocuments.Include(x => x.Client).FirstAsync(x => x.Id == doc.Id);
            Error = "No se pudo confirmar la firma y generar el PDF. Actualice la página y vuelva a intentar. Si continúa el problema, contacte a HN Solutions.";
            return Page();
        }
        if (ClientDocumentSigningService.IsCompleted(doc))
        {
            try { await signing.NotifyCompletedAsync(doc.Id); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Correo de confirmación pendiente para {DocumentId}", doc.Id);
                Notice = "Su firma quedó registrada. El PDF está disponible; el envío de la confirmación por correo quedó pendiente de reintento por Administración.";
            }
        }
        return RedirectToPage(new { token });
    }

    private static byte[]? ParseDataUrl(string? dataUrl)
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
