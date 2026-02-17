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
    private readonly IConfiguration _cfg;
    private readonly IServiceOrderPdfRenderer _pdf;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;

    public DetailsModel(ApplicationDbContext db, IConfiguration cfg, IServiceOrderPdfRenderer pdf, IFileStorage storage, IEmailSender email)
    {
        _db = db;
        _cfg = cfg;
        _pdf = pdf;
        _storage = storage;
        _email = email;
    }

    public ServiceOrder? Order { get; set; }
    public string ClientName { get; set; } = "";
    public string PublicUrl { get; set; } = "";
    public string? Info { get; set; }

    public record ChecklistRow(int Sort, string Title, bool Done, string Notes);
    public List<ChecklistRow> Checklist { get; set; } = new();

    public record EvidenceRow(Guid Id, string Name, string Date);
    public List<EvidenceRow> Evidences { get; set; } = new();

    public string TechSignature { get; set; } = "—";
    public string ClientSignature { get; set; } = "—";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadAsync(id);
        return Order == null ? NotFound() : Page();
    }

    // ✅ DESCARGA DIRECTO DESDE DETAILS (evita rutas /Admin/ServiceOrders/DownloadPdf)
    // URL: /Admin/ServiceOrders/Details/{id}?handler=DownloadPdf
    public async Task<IActionResult> OnGetDownloadPdfAsync(Guid id)
    {
        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound("Orden no encontrada.");

        if (string.IsNullOrWhiteSpace(o.PdfStoragePath))
            return NotFound("La orden no tiene PDF generado aún.");

        try
        {
            var (stream, contentType, downloadName) =
                await _storage.OpenAsync(o.PdfStoragePath, $"OrdenServicio_{id:N}.pdf");

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            // Fuerza descarga con nombre correcto
            return File(ms.ToArray(), contentType ?? "application/pdf", downloadName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("El archivo PDF no existe en el storage (ruta guardada inválida).");
        }
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync(Guid id)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        var pdfBytes = await _pdf.RenderAsync(Order);

        var (path, _, _) = await _storage.SaveBytesAsync(
            pdfBytes,
            $"serviceorders/{Order.Id}",
            $"order_{Order.Id:N}.pdf",
            "application/pdf");

        Order.PdfStoragePath = path;
        Order.PdfGeneratedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        Info = "PDF generado.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSendEmailAsync(Guid id)
    {
        await LoadAsync(id);
        if (Order == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Order.PdfStoragePath))
        {
            Info = "Primero genera el PDF.";
            return Page();
        }

        if (Order.Client?.Email == null || string.IsNullOrWhiteSpace(Order.Client.Email))
        {
            Info = "El cliente no tiene correo registrado.";
            return Page();
        }

        var (stream, contentType, _) = await _storage.OpenAsync(Order.PdfStoragePath, "OrdenServicio.pdf");
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        var html = $@"
<div style='font-family:Arial'>
  <h2>Orden de Servicio</h2>
  <p><b>Cliente:</b> {Order.Client.Name}</p>
  <p><b>Título:</b> {Order.Title}</p>
  <p>Adjunto PDF de cierre.</p>
</div>";

        await _email.SendAsync(
            Order.Client.Email,
            $"Orden de Servicio - {Order.Title}",
            html,
            ms.ToArray(),
            "OrdenServicio.pdf",
            contentType);

        Info = $"Correo enviado a {Order.Client.Email}.";
        return Page();
    }

    private async Task LoadAsync(Guid id)
    {
        // ✅ BaseUrl con fallback al host actual (soporta /interno si usas PathBase)
        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        baseUrl = baseUrl.TrimEnd('/');

        Order = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Checklist)
            .Include(o => o.Evidences)
            .Include(o => o.Signatures)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (Order == null) return;

        ClientName = Order.Client?.Name ?? "";
        PublicUrl = $"{baseUrl}/Public/ServiceOrder/{Order.PublicToken}";

        Checklist = Order.Checklist
            .OrderBy(x => x.SortOrder)
            .Select(x => new ChecklistRow(x.SortOrder, x.Title, x.IsDone, x.Notes))
            .ToList();

        Evidences = Order.Evidences
            .OrderByDescending(e => e.UploadedAt)
            .Select(e => new EvidenceRow(e.Id, e.OriginalFileName, e.UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")))
            .ToList();

        var tech = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);
        var client = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Client);

        TechSignature = tech == null ? "—" : $"{tech.SignedByName} ({tech.SignedAt.ToLocalTime():yyyy-MM-dd HH:mm})";
        ClientSignature = client == null ? "—" : $"{client.SignedByName} ({client.SignedAt.ToLocalTime():yyyy-MM-dd HH:mm})";
    }
}
