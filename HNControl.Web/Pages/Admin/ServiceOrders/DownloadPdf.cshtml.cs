using HNControl.Web.Data;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = HNControl.Web.Models.AppRoles.Admin)]
public class DownloadPdfModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IServiceOrderPdfRenderer _pdf;
    private readonly IConfiguration _cfg;

    public DownloadPdfModel(ApplicationDbContext db, IFileStorage storage, IServiceOrderPdfRenderer pdf, IConfiguration cfg)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
        _cfg = cfg;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, bool inline = true, bool refresh = false)
    {
        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();

        // Si piden refresh o no existe PDF, lo regeneramos aquí mismo (para vista previa admin).
        if (refresh || string.IsNullOrWhiteSpace(o.PdfStoragePath))
        {
            var bytes = await _pdf.RenderAsync(o);

            var maxMb = _cfg.GetValue<int?>("Storage:MaxPdfMb") ?? 15;
            if (bytes.Length > maxMb * 1024 * 1024)
                return BadRequest($"El PDF excede el tamaño máximo permitido ({maxMb} MB).");

            var fileName = $"orden_{o.Id:N}.pdf";
            var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{o.Id}/pdf", fileName, "application/pdf");

            o.PdfStoragePath = path;
            o.PdfGeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (string.IsNullOrWhiteSpace(o.PdfStoragePath)) return NotFound();

        var downloadName = $"OrdenServicio_{id:N}.pdf";
        var (stream, _, _) = await _storage.OpenAsync(o.PdfStoragePath, downloadName);

        // En algunos proxies el streaming es raro; devolver bytes es más robusto.
        await using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            Response.Headers["Content-Disposition"] = inline
                ? $"inline; filename=\"{downloadName}\""
                : $"attachment; filename=\"{downloadName}\"";

            return File(ms.ToArray(), "application/pdf");
        }
    }
}
