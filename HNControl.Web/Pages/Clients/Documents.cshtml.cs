using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class DocumentsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IClientLegalPdfRenderer _pdf;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;

    public DocumentsModel(
        ApplicationDbContext db,
        IClientLegalPdfRenderer pdf,
        IFileStorage storage,
        IEmailSender email,
        IConfiguration cfg)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _email = email;
        _cfg = cfg;
    }

    public Client? Client { get; set; }
    public List<ClientServiceContract> Contracts { get; set; } = new();
    public List<LegalDocRow> Docs { get; set; } = new();

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    public record LegalDocRow(
        Guid Id,
        string Type,
        string Title,
        string Status,
        bool HasPdf,
        bool IsSigned,
        string CreatedAt,
        string? SignedAt,
        string? SignedBy,
        Guid? ContractId);

    public async Task<IActionResult> OnGetAsync(Guid clientId)
    {
        await LoadAsync(clientId);
        return Client == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(Guid clientId, ClientLegalDocumentType type, Guid? contractId)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == clientId);
        if (client == null) return NotFound();

        var contract = contractId.HasValue
            ? await _db.ClientServiceContracts.FirstOrDefaultAsync(x => x.Id == contractId.Value && x.ClientId == clientId)
            : null;

        var doc = new ClientLegalDocument
        {
            ClientId = clientId,
            ClientServiceContractId = contract?.Id,
            DocumentType = type,
            Status = ClientLegalDocumentStatus.Draft,
            Title = type == ClientLegalDocumentType.NDA
                ? $"NDA - {client.Name}"
                : $"Contrato de servicios - {client.Name}",
            TermsBody = BuildDefaultTerms(type, client, contract),
            MonthlyAmount = contract?.MonthlyAmount,
            ContractStartDate = contract?.ContractStartDate,
            ContractEndDate = contract?.ContractEndDate,
            PublicToken = Guid.NewGuid().ToString("N"),
            TokenExpiresAt = DateTime.UtcNow.AddMonths(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClientLegalDocuments.Add(doc);
        await _db.SaveChangesAsync();

        await RegeneratePdfAsync(doc);

        Flash = $"{(type == ClientLegalDocumentType.NDA ? "NDA" : "Contrato")} generado correctamente.";
        FlashType = "success";
        return RedirectToPage(new { clientId });
    }

    public async Task<IActionResult> OnPostSendForSignatureAsync(Guid clientId, Guid docId)
    {
        var doc = await _db.ClientLegalDocuments
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == docId && x.ClientId == clientId);
        if (doc == null) return NotFound();

        if (string.IsNullOrWhiteSpace(doc.PdfStoragePath))
            await RegeneratePdfAsync(doc);

        var recipient = (doc.Client?.LegalEmail ?? doc.Client?.Email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            Flash = "El cliente no tiene correo legal/correo principal para enviar firma.";
            FlashType = "danger";
            return RedirectToPage(new { clientId });
        }

        if (string.IsNullOrWhiteSpace(doc.PublicToken))
            doc.PublicToken = Guid.NewGuid().ToString("N");

        doc.Status = ClientLegalDocumentStatus.SentForSignature;
        doc.TokenExpiresAt = DateTime.UtcNow.AddMonths(2);
        doc.SentAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        var signUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? Url.Page("/Public/ClientDocument", pageHandler: null, values: new { token = doc.PublicToken }, protocol: Request.Scheme) ?? ""
            : $"{baseUrl}/Public/ClientDocument/{doc.PublicToken}";

        byte[]? attachment = null;
        string attachmentName = $"documento_{doc.Id:N}.pdf";
        if (!string.IsNullOrWhiteSpace(doc.PdfStoragePath))
        {
            var (stream, _, _) = await _storage.OpenAsync(doc.PdfStoragePath, attachmentName);
            await using (stream)
            await using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                attachment = ms.ToArray();
            }
        }

        await _email.SendAsync(
            recipient,
            $"Firma requerida: {doc.Title}",
            $"""
            <p>Hola {System.Net.WebUtility.HtmlEncode(doc.Client?.LegalRepresentative ?? doc.Client?.Name ?? "cliente")},</p>
            <p>Ya puedes revisar y firmar digitalmente el siguiente documento:</p>
            <p><a href="{signUrl}">{signUrl}</a></p>
            <p>Al firmarlo, recibirás el PDF actualizado con la firma digital.</p>
            <p>Saludos,<br/>HN Control</p>
            """,
            attachment,
            attachmentName,
            "application/pdf");

        Flash = "Documento enviado para firma digital.";
        FlashType = "success";
        return RedirectToPage(new { clientId });
    }

    public async Task<IActionResult> OnPostDownloadAsync(Guid clientId, Guid docId)
    {
        var doc = await _db.ClientLegalDocuments.FirstOrDefaultAsync(x => x.Id == docId && x.ClientId == clientId);
        if (doc == null) return NotFound();
        if (string.IsNullOrWhiteSpace(doc.PdfStoragePath)) return BadRequest("No hay PDF.");

        var safeName = $"{(doc.DocumentType == ClientLegalDocumentType.NDA ? "nda" : "contrato")}_{doc.Id:N}.pdf";
        var (stream, contentType, _) = await _storage.OpenAsync(doc.PdfStoragePath, safeName);
        return File(stream, contentType, safeName);
    }

    public async Task<IActionResult> OnPostRegeneratePdfAsync(Guid clientId, Guid docId)
    {
        var doc = await _db.ClientLegalDocuments.FirstOrDefaultAsync(x => x.Id == docId && x.ClientId == clientId);
        if (doc == null) return NotFound();

        await RegeneratePdfAsync(doc);
        Flash = "PDF regenerado.";
        FlashType = "success";
        return RedirectToPage(new { clientId });
    }

    private async Task LoadAsync(Guid clientId)
    {
        Client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId);
        if (Client == null) return;

        Contracts = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        Docs = await _db.ClientLegalDocuments
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new LegalDocRow(
                x.Id,
                x.DocumentType == ClientLegalDocumentType.NDA ? "NDA" : "Contrato",
                x.Title,
                x.Status == ClientLegalDocumentStatus.Draft ? "Borrador" :
                x.Status == ClientLegalDocumentStatus.SentForSignature ? "En firma" : "Firmado",
                !string.IsNullOrWhiteSpace(x.PdfStoragePath),
                x.Status == ClientLegalDocumentStatus.Signed,
                x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                x.SignedAt.HasValue ? x.SignedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : null,
                x.SignedByName,
                x.ClientServiceContractId
            ))
            .ToListAsync();
    }

    private async Task RegeneratePdfAsync(ClientLegalDocument doc)
    {
        var pdfBytes = await _pdf.RenderAsync(doc);
        var fileName = $"legal_{doc.DocumentType}_{doc.Id:N}.pdf";
        var (path, _, _) = await _storage.SaveBytesAsync(pdfBytes, $"clients/{doc.ClientId}/legal", fileName, "application/pdf");
        doc.PdfStoragePath = path;
        doc.PdfGeneratedAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static string BuildDefaultTerms(ClientLegalDocumentType type, Client client, ClientServiceContract? contract)
    {
        if (type == ClientLegalDocumentType.NDA)
        {
            return
                $"Las partes, HN Solutions y {client.Name}, acuerdan mantener confidencial toda informacion tecnica, comercial y operativa compartida durante la relacion de servicio. " +
                "La informacion no podra ser divulgada a terceros sin autorizacion escrita. Este acuerdo permanece vigente durante la relacion comercial y por 24 meses posteriores a su terminacion.";
        }

        var serviceName = contract?.Label ?? "servicios de tecnologia";
        return
            $"HN Solutions prestara a {client.Name} el servicio '{serviceName}' bajo los alcances establecidos por ambas partes. " +
            "El cliente se compromete a proporcionar accesos y facilidades operativas para la correcta ejecucion. " +
            "Los pagos, vigencia y condiciones de renovacion se regiran por los datos comerciales capturados en este documento.";
    }
}
