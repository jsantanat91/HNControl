using HNControl.Web.Data;
using System.Security.Claims;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Delivery;

[Authorize(Policy = "EmployeeOnly")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly DeliveryDocumentSigningService _signing;
    private readonly DocumentNotificationRecipients _recipients;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext db,
        IFileStorage storage,
        IEmailSender email,
        IConfiguration cfg,
        DeliveryDocumentSigningService signing,
        DocumentNotificationRecipients recipients,
        ILogger<DetailsModel> logger)
    {
        _db = db;
        _storage = storage;
        _email = email;
        _cfg = cfg;
        _signing = signing;
        _recipients = recipients;
        _logger = logger;
    }

    public ProjectDeliveryFormat? Item { get; set; }
    public Guid? BackClientId { get; set; }
    public string? PublicSignUrl { get; set; }
    public string ServiceSummaryDisplay { get; set; } = "-";
    public string EquipmentSummaryDisplay { get; set; } = "-";
    public string SegmentoLanDisplay { get; set; } = "-";
    public string IpPublicaDisplay { get; set; } = "-";
    public List<DeliveryEvidenceRow> EvidenceItems { get; set; } = [];

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, Guid? clientId)
    {
        BackClientId = clientId;
        await LoadAsync(id);
        return Item == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        try
        {
            await RegeneratePdfAsync(item);
            Flash = "PDF generado desde la plantilla Word.";
            FlashType = "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando acta {DeliveryId}", id);
            Flash = "No se pudo generar el PDF. Las actas con firmas no se modifican; para borradores, verifique LibreOffice en el servidor.";
            FlashType = "danger";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSendForSignatureAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        if (item.SignedAt.HasValue)
        {
            Flash = "La firma del cliente ya está registrada. Complete la firma HN o envíe la confirmación final.";
            FlashType = "info";
            return RedirectToPage(new { id });
        }
        if (string.IsNullOrWhiteSpace(item.PdfStoragePath))
        {
            try { await RegeneratePdfAsync(item); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparando acta {DeliveryId}", id);
                Flash = "No se pudo preparar el acta desde la plantilla Word. Verifique LibreOffice antes de enviar la firma.";
                FlashType = "danger";
                return RedirectToPage(new { id });
            }
        }

        if (!ClientCommunicationTemplates.IsEmail(item.ReceiverEmail))
        {
            Flash = "El formato no tiene correo de receptor.";
            FlashType = "danger";
            return RedirectToPage(new { id });
        }

        item.Status = ProjectDeliveryFormatStatus.SentForSignature;
        item.TokenExpiresAt = DateTime.UtcNow.AddMonths(2);
        item.SentAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(item.PublicToken))
            item.PublicToken = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync();

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        var signUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? Url.Page("/Public/DeliveryDocument", pageHandler: null, values: new { token = item.PublicToken }, protocol: Request.Scheme) ?? ""
            : $"{baseUrl}/Public/DeliveryDocument/{item.PublicToken}";

        byte[]? attachment = null;
        if (!string.IsNullOrWhiteSpace(item.PdfStoragePath))
        {
            var (stream, _, _) = await _storage.OpenAsync(item.PdfStoragePath, $"acta_{item.Id:N}.pdf");
            await using (stream)
            await using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                attachment = ms.ToArray();
            }
        }

        try
        {
        await _email.SendAsync(
            item.ReceiverEmail,
            $"Firma requerida · {item.Title}",
            $"""
            <p>Hola {System.Net.WebUtility.HtmlEncode(item.ReceiverName)},</p>
            <p>Se generó un formato de entrega para tu firma digital:</p>
            <p><a href="{signUrl}">{signUrl}</a></p>
            <p>Al completar las firmas del cliente y HN Solutions, recibirás el acta final firmada en PDF.</p>
            """,
            attachment,
            $"acta_entrega_{item.Id:N}.pdf",
            "application/pdf", await _recipients.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando invitación de acta {DeliveryId}", id);
            Flash = "No se pudo confirmar el envío. Revise SMTP y los correos recibidos antes de reintentar.";
            FlashType = "warning";
            return RedirectToPage(new { id });
        }

        Flash = "Acta enviada para firma.";
        FlashType = "success";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDownloadPdfAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        if (string.IsNullOrWhiteSpace(item.PdfStoragePath)) return BadRequest("No hay PDF.");

        var (stream, contentType, _) = await _storage.OpenAsync(item.PdfStoragePath, $"acta_entrega_{id:N}.pdf");
        return File(stream, contentType, $"acta_entrega_{id:N}.pdf");
    }

    public async Task<IActionResult> OnGetDownloadEvidenceAsync(Guid id, string storagePath)
    {
        var item = await _db.ProjectDeliveryFormats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        var payload = ProjectDeliveryPayload.Parse(item.ServiceSummary);
        var evidence = payload.Evidences.FirstOrDefault(x => string.Equals(x.StoragePath, storagePath, StringComparison.Ordinal));
        if (evidence == null || string.IsNullOrWhiteSpace(evidence.StoragePath)) return NotFound();

        var (stream, contentType, _) = await _storage.OpenAsync(evidence.StoragePath, evidence.OriginalFileName ?? "evidencia");
        return File(stream, contentType, evidence.OriginalFileName ?? "evidencia");
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!AppRoles.IsGlobalAdmin(User) && !User.IsInRole(AppRoles.Admin))
            return Forbid();

        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        var clientId = item.ClientId;
        _db.ProjectDeliveryFormats.Remove(item);
        await _db.SaveChangesAsync();
        TempData["ClientDetailsInfo"] = "Formato de entrega eliminado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage("/Clients/Details", new { id = clientId });
    }

    private async Task LoadAsync(Guid id)
    {
        Item = await _db.ProjectDeliveryFormats
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Item == null) return;

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        PublicSignUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? Url.Page("/Public/DeliveryDocument", pageHandler: null, values: new { token = Item.PublicToken }, protocol: Request.Scheme) ?? ""
            : $"{baseUrl}/Public/DeliveryDocument/{Item.PublicToken}";

        var payload = ProjectDeliveryPayload.Parse(Item.ServiceSummary);
        if (Item.ServiceSummary?.StartsWith(ProjectDeliveryPayload.Prefix, StringComparison.Ordinal) == true)
        {
            ServiceSummaryDisplay = ProjectDeliveryPayload.ServicesDisplay(payload);
            EquipmentSummaryDisplay = ProjectDeliveryPayload.EquipmentDisplay(payload);
        }
        else
        {
            ServiceSummaryDisplay = string.IsNullOrWhiteSpace(Item.ServiceSummary) ? "-" : Item.ServiceSummary;
            EquipmentSummaryDisplay = string.IsNullOrWhiteSpace(Item.EquipmentSummary) ? "-" : Item.EquipmentSummary;
        }

        SegmentoLanDisplay = ProjectDeliveryPayload.Safe(payload.SEGMENTOLAN);
        IpPublicaDisplay = ProjectDeliveryPayload.Safe(payload.IPPUBLICA);
        EvidenceItems = payload.Evidences
            .Where(x => !string.IsNullOrWhiteSpace(x.StoragePath))
            .Take(ProjectDeliveryPayload.MaxEvidenceFiles)
            .ToList();
    }

    private async Task RegeneratePdfAsync(ProjectDeliveryFormat item)
    {
        if (item.SignedAt.HasValue || item.ProviderSignedAt.HasValue)
            throw new InvalidOperationException("El acta tiene firmas y no puede regenerarse.");
        var dbItem = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstAsync(x => x.Id == item.Id);

        dbItem.SigningSnapshotJson = null;
        await _signing.SavePdfAsync(dbItem);
    }

    public async Task<IActionResult> OnPostSignProviderAsync(Guid id, bool acceptTerms)
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        if (!acceptTerms) return BadRequest("Confirme la aceptación del acta en representación de HN Solutions.");
        var item = await _db.ProjectDeliveryFormats.Include(x => x.Client).Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        if (!item.SignedAt.HasValue) return BadRequest("Primero debe firmar el cliente.");
        if (!item.ProviderSignedAt.HasValue)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var account = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (account == null || !ClientCommunicationTemplates.IsEmail(account.Email))
                return BadRequest("Su cuenta administradora debe tener un correo válido.");
            var name = await _db.EmployeeProfiles.Where(x => x.UserId == userId).Select(x => x.FullName).FirstOrDefaultAsync();
            item.ProviderSignedByName = string.IsNullOrWhiteSpace(name) ? account.UserName ?? account.Email : name;
            item.ProviderSignedByEmail = account.Email;
            item.ProviderSignedByUserId = account.Id;
            item.ProviderSignedAt = DateTime.UtcNow;
            try { await _signing.SavePdfAsync(item); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo guardar firma HN del acta {DeliveryId}", id);
                Flash = "No se pudo guardar la firma HN y su PDF. Actualice la página y vuelva a intentar; verifique el convertidor PDF si continúa el problema.";
                FlashType = "danger";
                return RedirectToPage(new { id });
            }
        }
        return await SendFinalConfirmationAsync(id);
    }

    public async Task<IActionResult> OnPostSendConfirmationAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        if (!item.IsFullySigned) return BadRequest("El acta requiere ambas firmas.");
        return await SendFinalConfirmationAsync(id);
    }

    private async Task<IActionResult> SendFinalConfirmationAsync(Guid id)
    {
        try
        {
            await _signing.NotifyCompletedAsync(id);
            Flash = "Acta firmada. Confirmación enviada al cliente y a los destinatarios internos configurados.";
            FlashType = "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confirmación pendiente del acta {DeliveryId}", id);
            Flash = "Ambas firmas y el PDF están guardados, pero el correo quedó pendiente. Revise SMTP y use Enviar confirmación final para reintentar.";
            FlashType = "warning";
        }
        return RedirectToPage(new { id });
    }

}


