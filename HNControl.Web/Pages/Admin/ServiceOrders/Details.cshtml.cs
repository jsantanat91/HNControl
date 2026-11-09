using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceOrderPdfRenderer _pdf;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly ServiceOrderCreationNotifications _notifications;

    public DetailsModel(ApplicationDbContext db, IServiceOrderPdfRenderer pdf, IFileStorage storage, IEmailSender email, IConfiguration cfg, ServiceOrderCreationNotifications notifications)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _email = email;
        _cfg = cfg;
        _notifications = notifications;
    }

    public ServiceOrder? Order { get; set; }
    public string PublicUrl { get; set; } = "";
    public string? Info { get; set; }
    public string PdfEmailRecipient { get; private set; } = "";
    public IReadOnlyCollection<string> PdfEmailCopies { get; private set; } = Array.Empty<string>();
    public string? PdfEmailConfigurationError { get; private set; }
    public ServiceOrderNotificationState CreationNotifications { get; set; } = new(false, false, false, false);

    public async Task<IActionResult> OnPostRetryCreationNotificationsAsync(Guid id)
    {
        if (!User.IsInRole(AppRoles.Admin)) return Forbid();
        if (!await _db.ServiceOrders.AnyAsync(x => x.Id == id)) return NotFound();
        TempData[ServiceOrderCreationNotifications.NoticeKey] = (await _notifications.NotifyCreatedAsync(id)).UserMessage;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadAsync(id);
        return Order == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync(Guid id)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        var bytes = await _pdf.RenderAsync(Order);

        var maxMb = _cfg.GetValue<int?>("Storage:MaxPdfMb") ?? 15;
        if (bytes.Length > maxMb * 1024 * 1024)
        {
            Info = $"El PDF excede el tamaño máximo permitido ({maxMb} MB).";
            await LoadAsync(id);
            return Page();
        }

        var fileName = $"orden_{Order.Id:N}.pdf";
        var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{Order.Id}/pdf", fileName, "application/pdf");

        Order.PdfStoragePath = path;
        Order.PdfGeneratedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Info = "PDF generado.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostDownloadPdfAsync(Guid id)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Order.PdfStoragePath))
            return BadRequest("No hay PDF generado aún.");

        var (stream, contentType, originalName) = await _storage.OpenAsync(Order.PdfStoragePath, $"orden_{Order.Id:N}.pdf");
        return File(stream, contentType, originalName);
    }
    public async Task<IActionResult> OnPostSendEmailAsync(Guid id)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Order.PdfStoragePath))
        {
            Info = "Primero genera el PDF.";
            await LoadAsync(id);
            return Page();
        }

        if (PdfEmailConfigurationError != null)
        {
            Info = PdfEmailConfigurationError;
            return Page();
        }
        if (!ClientCommunicationTemplates.IsEmail(PdfEmailRecipient))
        {
            Info = "Registra un correo válido en la ficha del cliente antes de enviar la orden.";
            return Page();
        }

        // Render the current data and signatures, rather than sending an outdated cached PDF.
        var bytes = await _pdf.RenderAsync(Order);
        var maxMb = _cfg.GetValue<int?>("Storage:MaxPdfMb") ?? 15;
        if (bytes.Length > maxMb * 1024 * 1024)
        {
            Info = $"El PDF excede el tamaño máximo permitido ({maxMb} MB).";
            return Page();
        }
        var fileName = $"orden_{Order.Id:N}.pdf";
        var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{Order.Id}/pdf", fileName, "application/pdf");
        Order.PdfStoragePath = path;
        Order.PdfGeneratedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _email.SendAsync(
                toEmail: PdfEmailRecipient,
                subject: $"HN Solutions · Orden de servicio {ServiceOrderNotificationTemplates.Reference(Order)} · {Order.Title}".Replace('\r', ' ').Replace('\n', ' '),
                htmlBody: ServiceOrderNotificationTemplates.Report(Order),
                attachmentBytes: bytes,
                attachmentName: fileName,
                attachmentContentType: "application/pdf",
                ccEmails: PdfEmailCopies
            );
            Info = $"Correo enviado a {PdfEmailRecipient}. " + (PdfEmailCopies.Count > 0
                ? $"Copia (CC): {string.Join(", ", PdfEmailCopies)}."
                : "Sin copias: no hay otros destinatarios de Ingeniería configurados.");
        }
        catch (InvalidOperationException)
        {
            Info = "No se pudo confirmar el envío del correo. Revisa SMTP y los destinatarios antes de reintentar.";
        }
        return Page();
    }


    public async Task<IActionResult> OnPostApproveAsync(Guid id, string? ReviewNotes)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        Order.AdminReviewNotes = (ReviewNotes ?? "").Trim();
        Order.Status = ServiceOrderStatus.Finalized;
        Order.FinalizedAt = DateTime.UtcNow;

        // ✅ Guardamos primero para que el PDF refleje status/notes actuales.
        await _db.SaveChangesAsync();

        // ✅ Siempre regenerar PDF al aprobar (evita PDF viejo sin notas de checklist).
        var bytes = await _pdf.RenderAsync(Order);
        var fileName = $"orden_{Order.Id:N}.pdf";
        var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{Order.Id}/pdf", fileName, "application/pdf");
        Order.PdfStoragePath = path;
        Order.PdfGeneratedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        Info = "Orden aprobada y finalizada (PDF actualizado).";
        await LoadAsync(id);
        return Page();
    }


    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? ReviewNotes)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        Order.AdminReviewNotes = (ReviewNotes ?? "").Trim();
        Order.Status = ServiceOrderStatus.Rejected;

        // La regresamos a “en proceso” por si quieren seguir trabajando y re-enviar
        Order.SubmittedForReviewAt = null;
        Order.FinalizedAt = null;
        Order.CurrentArea = ServiceOrderWorkflowArea.Levantamiento;

        // ✅ Invalida PDF cacheado
        Order.PdfStoragePath = null;
        Order.PdfGeneratedAt = null;

        // ✅ Fuerza nueva firma técnica después de rechazo (evita reenvío accidental con firma anterior)
        if (Order.Signatures?.Count > 0)
            _db.ServiceOrderSignatures.RemoveRange(Order.Signatures);

        await _db.SaveChangesAsync();

        Info = "Orden rechazada. Regresó a Levantamiento y requiere nueva firma técnica para reenviar.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostUnassignTechnicianAsync(Guid id)
    {
        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        if (order.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
        {
            Info = "La orden ya no permite desasignar técnico.";
            await LoadAsync(id);
            return Page();
        }

        order.ClaimedByUserId = null;
        order.ClaimedAt = null;
        await _db.SaveChangesAsync();

        Info = "Técnico desasignado. La orden quedó disponible para que otro la tome.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostApplyChecklistTemplateAsync(Guid id)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        var before = Order.Checklist.Count;

        // Checklist general (si no es Global)
        if (Order.Type != ServiceOrderType.Global)
            await EnsureChecklistFromTemplateAsync(Order, Order.Type, workItemId: null);

        // Si es Global: por actividad
        if (Order.WorkItems != null && Order.WorkItems.Count > 0)
        {
            foreach (var w in Order.WorkItems.OrderBy(x => x.SortOrder))
                await EnsureChecklistFromTemplateAsync(Order, w.Type, workItemId: w.Id);
        }

        var added = Order.Checklist.Count - before;
        if (added > 0)
        {
            // ✅ Invalida PDF cacheado
            Order.PdfStoragePath = null;
            Order.PdfGeneratedAt = null;

            await _db.SaveChangesAsync();
            Info = $"Checklist cargado desde plantilla (+{added} ítem(s)).";
        }
        else
        {
            Info = "No se agregó checklist (ya existía o no hay plantilla para ese tipo).";
        }

        await LoadAsync(id);
        return Page();
    }

    private async Task EnsureChecklistFromTemplateAsync(ServiceOrder order, ServiceOrderType type, Guid? workItemId)
    {
        if (order.Checklist.Any(x => x.WorkItemId == workItemId))
            return;

        var template = await _db.ServiceOrderChecklistTemplates
            .Include(t => t.Items)
            .Where(t => t.IsActive && t.Type == type)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync();

        if (template == null || template.Items.Count == 0)
            return;

        foreach (var it in template.Items.OrderBy(x => x.SortOrder))
        {
            order.Checklist.Add(new ServiceOrderChecklistItem
            {
                OrderId = order.Id,
                WorkItemId = workItemId,
                SortOrder = it.SortOrder,
                Category = it.Category,
                Title = it.Title,
                IsRequired = it.IsRequired,
                IsDone = false,
                Notes = ""
            });
        }
    }

    private async Task LoadAsync(Guid id)
    {
        Order = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Project)
            .Include(o => o.ClientServiceContract)
            .Include(o => o.Checklist)
            .Include(o => o.Evidences)
            .Include(o => o.Signatures)
            .Include(o => o.WorkItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        PdfEmailRecipient = Order?.Client == null ? "" : ClientCommunicationTemplates.Recipient(Order.Client);
        try
        {
            if (Order != null) CreationNotifications = await _notifications.GetStateAsync(id);
            PdfEmailCopies = (await _notifications.EngineeringRecipientsAsync())
                .Where(x => !x.Equals(PdfEmailRecipient, StringComparison.OrdinalIgnoreCase)).ToArray();
            PdfEmailConfigurationError = null;
        }
        catch (ArgumentException)
        {
            PdfEmailCopies = Array.Empty<string>();
            PdfEmailConfigurationError = "Revisa los correos de Ingeniería en Configuración: hay una dirección inválida.";
        }

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        if (Order != null && !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(Order.PublicToken))
            PublicUrl = $"{baseUrl}/Public/ServiceOrder/{Order.PublicToken}";
    }
}
