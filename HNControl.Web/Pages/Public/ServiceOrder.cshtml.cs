using HNControl.Web.Data;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

public class ServiceOrderModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IServiceOrderPdfRenderer _pdf;
    private readonly IConfiguration _cfg;

    public ServiceOrderModel(ApplicationDbContext db, IFileStorage storage, IServiceOrderPdfRenderer pdf, IConfiguration cfg)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
        _cfg = cfg;
    }

    public string Token { get; set; } = "";
    public Models.ServiceOrder? Order { get; set; }
    public bool CanDownloadPdf => Order != null; // si no existe aún, se genera al vuelo en DownloadPdf

    public async Task<IActionResult> OnGetAsync(string token)
    {
        Token = token;

        Order = await _db.ServiceOrders
            .Include(o => o.Client)
            .FirstOrDefaultAsync(o => o.PublicToken == token);

        return Order == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(string token)
    {
        Order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.PublicToken == token);
        if (Order == null) return NotFound();

        // ✅ Si no existe PDF todavía, lo generamos al vuelo (para que el link sea realmente útil).
        if (string.IsNullOrWhiteSpace(Order.PdfStoragePath))
        {
            var bytes = await _pdf.RenderAsync(Order);

            var maxMb = _cfg.GetValue<int?>("Storage:MaxPdfMb") ?? 15;
            if (bytes.Length > maxMb * 1024 * 1024)
                return BadRequest($"El PDF excede el tamaño máximo permitido ({maxMb} MB).");

            var fileName = $"orden_{Order.Id:N}.pdf";
            var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{Order.Id}/pdf", fileName, "application/pdf");

            Order.PdfStoragePath = path;
            Order.PdfGeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var (stream, contentType, originalName) = await _storage.OpenAsync(Order.PdfStoragePath, $"orden_{Order.Id:N}.pdf");

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(stream, contentType, originalName);
    }
}
