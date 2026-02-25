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

    public DetailsModel(ApplicationDbContext db, IServiceOrderPdfRenderer pdf, IFileStorage storage, IEmailSender email, IConfiguration cfg)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _email = email;
        _cfg = cfg;
    }

    public ServiceOrder? Order { get; set; }
    public string PublicUrl { get; set; } = "";
    public string? Info { get; set; }

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

        var to = Order.Client?.Email;
        if (string.IsNullOrWhiteSpace(to))
        {
            Info = "El cliente no tiene email.";
            await LoadAsync(id);
            return Page();
        }

        var (stream, contentType, fileName) = await _storage.OpenAsync(Order.PdfStoragePath, $"orden_{Order.Id:N}.pdf");
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        await _email.SendAsync(
            toEmail: to,
            subject: $"Orden de Servicio - {Order.Title}",
            htmlBody: $"Adjunto PDF de la orden: <b>{Order.Title}</b>",
            attachmentBytes: ms.ToArray(),
            attachmentName: fileName,
            attachmentContentType: contentType
        );

        Info = "Correo enviado.";
        await LoadAsync(id);
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
            .FirstOrDefaultAsync(o => o.Id == id);

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        if (Order != null && !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(Order.PublicToken))
            PublicUrl = $"{baseUrl}/Public/ServiceOrder/{Order.PublicToken}";
    }
}
