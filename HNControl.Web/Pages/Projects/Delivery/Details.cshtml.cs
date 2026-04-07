using System.Text.Json;
using HNControl.Web.Data;
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
    private readonly IProjectDeliveryPdfRenderer _pdf;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly ITemplateDocxService _docxTemplates;
    private readonly IOfficePdfConverter _officePdfConverter;

    public DetailsModel(
        ApplicationDbContext db,
        IProjectDeliveryPdfRenderer pdf,
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

    public ProjectDeliveryFormat? Item { get; set; }
    public string? PublicSignUrl { get; set; }
    public string ServiceSummaryDisplay { get; set; } = "-";
    public string EquipmentSummaryDisplay { get; set; } = "-";

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadAsync(id);
        return Item == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        await RegeneratePdfAsync(item);
        Flash = "PDF generado.";
        FlashType = "success";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSendForSignatureAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        if (string.IsNullOrWhiteSpace(item.PdfStoragePath))
            await RegeneratePdfAsync(item);

        if (string.IsNullOrWhiteSpace(item.ReceiverEmail))
        {
            Flash = "El formato no tiene correo de receptor.";
            FlashType = "danger";
            return RedirectToPage(new { id });
        }

        item.Status = ProjectDeliveryFormatStatus.SentForSignature;
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

        await _email.SendAsync(
            item.ReceiverEmail,
            $"Firma requerida · {item.Title}",
            $"""
            <p>Hola {System.Net.WebUtility.HtmlEncode(item.ReceiverName)},</p>
            <p>Se generó un formato de entrega para tu firma digital:</p>
            <p><a href="{signUrl}">{signUrl}</a></p>
            <p>Al finalizar, recibirás el acta firmada en PDF.</p>
            """,
            attachment,
            $"acta_entrega_{item.Id:N}.pdf",
            "application/pdf");

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

        BuildDisplays(Item.ServiceSummary, Item.EquipmentSummary, out var services, out var equipment);
        ServiceSummaryDisplay = services;
        EquipmentSummaryDisplay = equipment;
    }

    private async Task RegeneratePdfAsync(ProjectDeliveryFormat item)
    {
        var dbItem = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstAsync(x => x.Id == item.Id);

        byte[]? bytes = null;
        if (dbItem.Client != null)
        {
            var docxBytes = _docxTemplates.BuildDeliveryDocx(dbItem, dbItem.Client, dbItem.Project);
            bytes = await _officePdfConverter.TryConvertDocxToPdfAsync(docxBytes, $"acta_{dbItem.Id:N}");
        }

        bytes ??= await _pdf.RenderAsync(item);
        var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"projects/delivery/{item.Id}", $"acta_{item.Id:N}.pdf", "application/pdf");
        item.PdfStoragePath = path;
        item.PdfGeneratedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static void BuildDisplays(string? serviceSummary, string? equipmentSummary, out string services, out string equipment)
    {
        if (string.IsNullOrWhiteSpace(serviceSummary) || !serviceSummary.StartsWith("__DELIVERYJSON__", StringComparison.Ordinal))
        {
            services = string.IsNullOrWhiteSpace(serviceSummary) ? "-" : serviceSummary;
            equipment = string.IsNullOrWhiteSpace(equipmentSummary) ? "-" : equipmentSummary;
            return;
        }

        try
        {
            var json = serviceSummary["__DELIVERYJSON__".Length..];
            var root = JsonSerializer.Deserialize<DeliveryTemplateData>(json) ?? new DeliveryTemplateData();

            var serviceLines = root.Services
                .Where(x => !string.IsNullOrWhiteSpace(x.Servicio) || !string.IsNullOrWhiteSpace(x.Modalidad) || !string.IsNullOrWhiteSpace(x.Plazo))
                .Select(x => $"{Safe(x.Servicio)} | {Safe(x.Modalidad)} | {Safe(x.Plazo)}")
                .ToList();

            var equipmentLines = root.Equipment
                .Where(x => !string.IsNullOrWhiteSpace(x.Equipo) || !string.IsNullOrWhiteSpace(x.Cantidad))
                .Select(x => $"{Safe(x.Equipo)} ({Safe(x.Cantidad)})")
                .ToList();

            services = serviceLines.Count == 0 ? "-" : string.Join("\n", serviceLines);
            equipment = equipmentLines.Count == 0 ? "-" : string.Join("\n", equipmentLines);
        }
        catch
        {
            services = "-";
            equipment = string.IsNullOrWhiteSpace(equipmentSummary) ? "-" : equipmentSummary;
        }
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private sealed class DeliveryTemplateData
    {
        public List<DeliveryServiceRow> Services { get; set; } = [];
        public List<DeliveryEquipmentRow> Equipment { get; set; } = [];
    }

    private sealed class DeliveryServiceRow
    {
        public string? Servicio { get; set; }
        public string? Modalidad { get; set; }
        public string? Plazo { get; set; }
    }

    private sealed class DeliveryEquipmentRow
    {
        public string? Equipo { get; set; }
        public string? Cantidad { get; set; }
    }
}
