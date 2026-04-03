using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

public class DeliveryDocumentModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IProjectDeliveryPdfRenderer _pdf;
    private readonly IEmailSender _email;

    public DeliveryDocumentModel(ApplicationDbContext db, IFileStorage storage, IProjectDeliveryPdfRenderer pdf, IEmailSender email)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
        _email = email;
    }

    public ProjectDeliveryFormat? Item { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        Item = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        return Item == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(string token)
    {
        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.PublicToken == token);
        if (item == null) return NotFound();
        if (string.IsNullOrWhiteSpace(item.PdfStoragePath)) return BadRequest("Aún no hay PDF.");
        var (stream, contentType, _) = await _storage.OpenAsync(item.PdfStoragePath, $"acta_entrega_{item.Id:N}.pdf");
        return File(stream, contentType, $"acta_entrega_{item.Id:N}.pdf");
    }

    public async Task<IActionResult> OnPostSignAsync(string token, string signerName, string signerEmail, string sigDataUrl)
    {
        var item = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        if (item == null) return NotFound();

        signerName = (signerName ?? "").Trim();
        signerEmail = (signerEmail ?? "").Trim();
        if (string.IsNullOrWhiteSpace(signerName) || string.IsNullOrWhiteSpace(signerEmail))
        {
            Item = item;
            Error = "Nombre y correo son obligatorios.";
            return Page();
        }

        var bytes = ParseDataUrl(sigDataUrl);
        if (bytes == null || bytes.Length < 200)
        {
            Item = item;
            Error = "Firma inválida.";
            return Page();
        }

        var (sigPath, _, _) = await _storage.SaveBytesAsync(bytes, $"projects/delivery/{item.Id}/signatures", $"sig_{DateTime.UtcNow:yyyyMMddHHmmss}.png", "image/png");
        item.SignatureStoragePath = sigPath;
        item.SignedByName = signerName;
        item.SignedByEmail = signerEmail;
        item.SignedAt = DateTime.UtcNow;
        item.Status = ProjectDeliveryFormatStatus.Signed;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var pdfBytes = await _pdf.RenderAsync(item);
        var (pdfPath, _, _) = await _storage.SaveBytesAsync(pdfBytes, $"projects/delivery/{item.Id}", $"acta_{item.Id:N}.pdf", "application/pdf");
        item.PdfStoragePath = pdfPath;
        item.PdfGeneratedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _email.SendAsync(
                signerEmail,
                $"Acta firmada: {item.Title}",
                $"""
                <p>Gracias {System.Net.WebUtility.HtmlEncode(signerName)},</p>
                <p>Tu firma digital quedó registrada correctamente.</p>
                <p>Adjuntamos el acta firmada en PDF.</p>
                """,
                pdfBytes,
                $"acta_firmada_{item.Id:N}.pdf",
                "application/pdf");
        }
        catch
        {
        }

        return RedirectToPage(new { token });
    }

    private static byte[]? ParseDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return null;
        try
        {
            return Convert.FromBase64String(dataUrl[(comma + 1)..]);
        }
        catch
        {
            return null;
        }
    }
}
