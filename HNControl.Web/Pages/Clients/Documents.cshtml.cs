using System.Text.Json;
using System.Globalization;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class DocumentsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IClientLegalPdfRenderer _pdf;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly ITemplateDocxService _docxTemplates;
    private readonly IOfficePdfConverter _officePdfConverter;

    public DocumentsModel(
        ApplicationDbContext db,
        IClientLegalPdfRenderer pdf,
        IFileStorage storage,
        IEmailSender email,
        IConfiguration cfg,
        ITemplateDocxService docxTemplates,
        IOfficePdfConverter officePdfConverter)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _email = email;
        _cfg = cfg;
        _docxTemplates = docxTemplates;
        _officePdfConverter = officePdfConverter;
    }

    public Client? Client { get; set; }
    public List<ClientServiceContract> Contracts { get; set; } = new();
    public List<LegalDocRow> Docs { get; set; } = new();
    public List<SignContactOption> SignContacts { get; set; } = new();

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
        Guid? ContractId,
        string? ContractLabel);

    public record SignContactOption(
        Guid Id,
        string Label,
        string? Email,
        string? Phone,
        bool IsPrimary);

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

        var doc = await _db.ClientLegalDocuments
            .FirstOrDefaultAsync(x =>
                x.ClientId == clientId
                && x.DocumentType == type
                && x.ClientServiceContractId == contractId);

        if (doc == null)
        {
            doc = new ClientLegalDocument
            {
                ClientId = clientId,
                DocumentType = type,
                ClientServiceContractId = contractId,
                PublicToken = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow
            };
            _db.ClientLegalDocuments.Add(doc);
        }

        doc.Status = ClientLegalDocumentStatus.Draft;
        var documentSubject = FirstNonEmpty(contract?.Label, client.Name);
        doc.Title = type == ClientLegalDocumentType.NDA
            ? $"NDA - {documentSubject}"
            : $"Contrato de servicios - {documentSubject}";
        doc.TermsBody = BuildTemplateTerms(type, client, contract);
        doc.MonthlyAmount = contract?.MonthlyAmount;
        doc.ContractStartDate = contract?.ContractStartDate;
        doc.ContractEndDate = contract?.ContractEndDate;
        doc.TokenExpiresAt = DateTime.UtcNow.AddMonths(2);
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        try
        {
            await RegeneratePdfAsync(doc);
        }
        catch (Exception ex)
        {
            Flash = $"Documento guardado, pero no se pudo convertir la plantilla Word a PDF: {ex.Message}";
            FlashType = "warning";
            return RedirectToPage(new { clientId });
        }

        Flash = $"{(type == ClientLegalDocumentType.NDA ? "NDA" : "Contrato")} generado correctamente.";
        FlashType = "success";
        return RedirectToPage(new { clientId });
    }

    public async Task<IActionResult> OnPostSendForSignatureAsync(Guid clientId, Guid docId, Guid? contactId)
    {
        var doc = await _db.ClientLegalDocuments
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == docId && x.ClientId == clientId);
        if (doc == null) return NotFound();

        if (string.IsNullOrWhiteSpace(doc.PdfStoragePath))
        {
            try
            {
                await RegeneratePdfAsync(doc);
            }
            catch (Exception ex)
            {
                Flash = $"No se pudo generar el PDF desde la plantilla Word: {ex.Message}";
                FlashType = "danger";
                return RedirectToPage(new { clientId });
            }
        }

        ClientContact? selectedContact = null;
        if (contactId.HasValue && contactId.Value != Guid.Empty)
        {
            selectedContact = await _db.ClientContacts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClientId == clientId && x.Id == contactId.Value);
            if (selectedContact == null)
            {
                Flash = "El contacto seleccionado no pertenece al cliente.";
                FlashType = "danger";
                return RedirectToPage(new { clientId });
            }
        }

        var recipient = FirstNonEmpty(selectedContact?.Email, doc.Client?.BillingEmail, doc.Client?.Email);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            Flash = "El contacto seleccionado no tiene correo (o el cliente no tiene correo de facturacion/principal).";
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
        var attachmentName = $"documento_{doc.Id:N}.pdf";
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

        var recipientName = FirstNonEmpty(
            selectedContact?.Name,
            doc.Client?.LegalRepresentative,
            doc.Client?.ContactName,
            doc.Client?.Name,
            "cliente");

        await _email.SendAsync(
            recipient,
            $"Firma requerida: {doc.Title}",
            $"""
            <p>Hola {System.Net.WebUtility.HtmlEncode(recipientName)},</p>
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

        try
        {
            await RegeneratePdfAsync(doc);
            Flash = "PDF regenerado desde plantilla Word.";
            FlashType = "success";
        }
        catch (Exception ex)
        {
            Flash = $"No se pudo convertir la plantilla Word a PDF: {ex.Message}";
            FlashType = "danger";
        }
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

        SignContacts = await _db.ClientContacts
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Name)
            .Select(x => new SignContactOption(
                x.Id,
                string.IsNullOrWhiteSpace(x.Role)
                    ? x.Name
                    : $"{x.Name} ({x.Role})",
                x.Email,
                x.Phone,
                x.IsPrimary))
            .ToListAsync();

        Docs = await _db.ClientLegalDocuments
            .AsNoTracking()
            .Include(x => x.ClientServiceContract)
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
                x.ClientServiceContractId,
                x.ClientServiceContract != null ? x.ClientServiceContract.Label : null
            ))
            .ToListAsync();
    }

    private async Task RegeneratePdfAsync(ClientLegalDocument doc)
    {
        var dbDoc = await _db.ClientLegalDocuments
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .FirstAsync(x => x.Id == doc.Id);

        byte[]? pdfBytes = null;
        if (dbDoc.Client != null)
        {
            var docxBytes = _docxTemplates.BuildClientLegalDocx(dbDoc, dbDoc.Client, dbDoc.ClientServiceContract);
            pdfBytes = await _officePdfConverter.TryConvertDocxToPdfAsync(docxBytes, $"legal_{dbDoc.DocumentType}_{dbDoc.Id:N}");
        }

        if (pdfBytes == null || pdfBytes.Length == 0)
            throw new InvalidOperationException("No fue posible convertir DOCX a PDF. Verifica que LibreOffice/soffice esté instalado y disponible en PATH.");

        var fileName = $"legal_{doc.DocumentType}_{doc.Id:N}.pdf";
        var (path, _, _) = await _storage.SaveBytesAsync(pdfBytes, $"clients/{doc.ClientId}/legal", fileName, "application/pdf");
        doc.PdfStoragePath = path;
        doc.PdfGeneratedAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static string BuildTemplateTerms(ClientLegalDocumentType type, Client client, ClientServiceContract? contract)
    {
        var technical = ClientServiceContractMetadata.ParseTechnical(contract?.Notes);
        var payload = new ContractTemplatePayload
        {
            RSCLIENTE = Safe(client.Name),
            RLCLIENTE = Safe(client.LegalRepresentative, client.ContactName),
            RFCC = Safe(client.Rfc),
            DIRECCIONC = Safe(client.FiscalAddress, client.Address),
            ESTADOC = Safe(client.State, "México"),
            CPC = Safe(client.FiscalZipCode),
            EMAILC = Safe(client.BillingEmail, client.Email),
            CONTRATOC = Safe(type == ClientLegalDocumentType.NDA ? "NDA" : contract?.Label, "Contrato de servicios"),
            PERIODOC = BuildPeriod(contract),
            SUCURSALC = Safe(contract?.Branch, contract?.Label),
            COSTOCLIENTE = (contract?.MonthlyAmount ?? 0m).ToString("N2", new CultureInfo("es-MX")),
            COSTOINST = technical.InstallationCost.ToString("N2", new CultureInfo("es-MX")),
            FIRMACLIENTE = "PENDIENTE DE FIRMA",
            NOMBREPROYECTO = Safe(contract?.Label),
            NOMBRETECNICO = "-"
        };

        return "__TPLJSON__" + JsonSerializer.Serialize(payload);
    }

    private static string BuildPeriod(ClientServiceContract? contract)
    {
        if (contract?.ContractStartDate.HasValue == true || contract?.ContractEndDate.HasValue == true)
        {
            var start = contract.ContractStartDate?.ToString("dd/MM/yyyy") ?? "-";
            var end = contract.ContractEndDate?.ToString("dd/MM/yyyy") ?? "-";
            return $"{start} al {end}";
        }

        return "12 meses";
    }

    private static string Safe(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "-";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private sealed class ContractTemplatePayload
    {
        public string? RSCLIENTE { get; set; }
        public string? RLCLIENTE { get; set; }
        public string? RFCC { get; set; }
        public string? DIRECCIONC { get; set; }
        public string? ESTADOC { get; set; }
        public string? CPC { get; set; }
        public string? EMAILC { get; set; }
        public string? CONTRATOC { get; set; }
        public string? PERIODOC { get; set; }
        public string? SUCURSALC { get; set; }
        public string? COSTOCLIENTE { get; set; }
        public string? COSTOINST { get; set; }
        public string? FIRMACLIENTE { get; set; }
        public string? NOMBREPROYECTO { get; set; }
        public string? NOMBRETECNICO { get; set; }
    }
}


