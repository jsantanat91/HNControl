using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

public class DeliveryDocumentModel(ApplicationDbContext db, IFileStorage storage,
    DeliveryDocumentSigningService signing, ILogger<DeliveryDocumentModel> logger) : PageModel
{
    public ProjectDeliveryFormat? Item { get; set; }
    public string? Error { get; set; }
    private static bool CanRead(ProjectDeliveryFormat item) => item.SignedAt.HasValue
        || (item.Status == ProjectDeliveryFormatStatus.SentForSignature && item.TokenExpiresAt > DateTime.UtcNow);

    public async Task<IActionResult> OnGetAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        Item = await db.ProjectDeliveryFormats.Include(x => x.Client).Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        return Item == null || !CanRead(Item) ? NotFound() : Page();
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var item = await db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.PublicToken == token);
        if (item == null || !CanRead(item)) return NotFound();
        if (string.IsNullOrWhiteSpace(item.PdfStoragePath)) return BadRequest("Aún no hay PDF.");
        var (stream, contentType, _) = await storage.OpenAsync(item.PdfStoragePath, $"acta_entrega_{item.Id:N}.pdf");
        return File(stream, contentType, $"acta_entrega_{item.Id:N}.pdf");
    }

    public async Task<IActionResult> OnPostSignAsync(string token, string signerName, string signerEmail, string sigDataUrl, bool acceptTerms)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var item = await db.ProjectDeliveryFormats.Include(x => x.Client).Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        if (item == null || !CanRead(item)) return NotFound();
        if (item.SignedAt.HasValue) return RedirectToPage(new { token });
        Item = item;
        signerName = (signerName ?? "").Trim();
        signerEmail = (signerEmail ?? "").Trim();
        if (signerName.Length is < 2 or > 200 || signerEmail.Length > 256
            || !ClientCommunicationTemplates.IsEmail(signerEmail) || !acceptTerms)
        {
            Error = "Ingrese su nombre y correo válidos y confirme la aceptación del acta.";
            return Page();
        }
        var bytes = SignatureDataUrl.ParsePng(sigDataUrl);
        if (bytes == null)
        {
            Error = "La firma es inválida. Vuelva a dibujarla.";
            return Page();
        }
        try
        {
            var (sigPath, _, _) = await storage.SaveBytesAsync(bytes, $"projects/delivery/{item.Id}/signatures", $"sig_{Guid.NewGuid():N}.png", "image/png");
            item.SignatureStoragePath = sigPath;
            item.SignedByName = signerName;
            item.SignedByEmail = signerEmail;
            item.SignedAt = DateTime.UtcNow;
            await signing.SavePdfAsync(item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo registrar firma de acta {DeliveryId}", item.Id);
            db.ChangeTracker.Clear();
            Item = await db.ProjectDeliveryFormats.Include(x => x.Client).Include(x => x.Project).FirstAsync(x => x.Id == item.Id);
            Error = "No se pudo confirmar la firma y generar el PDF. Actualice la página y vuelva a intentar. Si continúa el problema, contacte a HN Solutions.";
            return Page();
        }
        // The HN countersignature is performed after the client signature, from the internal detail page.
        return RedirectToPage(new { token });
    }
}
