using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

public class ClientDocumentModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IClientLegalPdfRenderer _pdf;
    private readonly IEmailSender _email;

    public ClientDocumentModel(ApplicationDbContext db, IFileStorage storage, IClientLegalPdfRenderer pdf, IEmailSender email)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
        _email = email;
    }

    public ClientLegalDocument? DocumentRef { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        DocumentRef = await _db.ClientLegalDocuments
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        return DocumentRef == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(string token)
    {
        var doc = await _db.ClientLegalDocuments.FirstOrDefaultAsync(x => x.PublicToken == token);
        if (doc == null) return NotFound();
        if (string.IsNullOrWhiteSpace(doc.PdfStoragePath)) return BadRequest("Aún no hay PDF.");

        var fileName = $"{(doc.DocumentType == ClientLegalDocumentType.NDA ? "nda" : "contrato")}_{doc.Id:N}.pdf";
        var (stream, contentType, _) = await _storage.OpenAsync(doc.PdfStoragePath, fileName);
        return File(stream, contentType, fileName);
    }

    public async Task<IActionResult> OnPostSignAsync(string token, string signerName, string signerEmail, string sigDataUrl)
    {
        var doc = await _db.ClientLegalDocuments
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.PublicToken == token);
        if (doc == null) return NotFound();

        signerName = (signerName ?? "").Trim();
        signerEmail = (signerEmail ?? "").Trim();

        if (string.IsNullOrWhiteSpace(signerName) || string.IsNullOrWhiteSpace(signerEmail))
        {
            DocumentRef = doc;
            Error = "Nombre y correo son obligatorios para firmar.";
            return Page();
        }

        var bytes = ParseDataUrl(sigDataUrl);
        if (bytes == null || bytes.Length < 200)
        {
            DocumentRef = doc;
            Error = "La firma es inválida, vuelve a dibujarla.";
            return Page();
        }

        var (sigPath, _, _) = await _storage.SaveBytesAsync(bytes, $"clients/{doc.ClientId}/legal/{doc.Id}/signatures", $"sig_{DateTime.UtcNow:yyyyMMddHHmmss}.png", "image/png");
        doc.SignatureStoragePath = sigPath;
        doc.SignedByName = signerName;
        doc.SignedByEmail = signerEmail;
        doc.SignedAt = DateTime.UtcNow;
        doc.Status = ClientLegalDocumentStatus.Signed;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var pdfBytes = await _pdf.RenderAsync(doc);
        var (pdfPath, _, _) = await _storage.SaveBytesAsync(pdfBytes, $"clients/{doc.ClientId}/legal", $"legal_{doc.DocumentType}_{doc.Id:N}.pdf", "application/pdf");
        doc.PdfStoragePath = pdfPath;
        doc.PdfGeneratedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _email.SendAsync(
                signerEmail,
                $"Documento firmado: {doc.Title}",
                $"""
                <p>Gracias {System.Net.WebUtility.HtmlEncode(signerName)},</p>
                <p>Se registró tu firma digital del documento <b>{System.Net.WebUtility.HtmlEncode(doc.Title)}</b>.</p>
                <p>Adjuntamos el PDF firmado para tu control.</p>
                """,
                pdfBytes,
                $"{doc.Title.Replace(' ', '_')}.pdf",
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
        var b64 = dataUrl[(comma + 1)..];
        try
        {
            return Convert.FromBase64String(b64);
        }
        catch
        {
            return null;
        }
    }
}
