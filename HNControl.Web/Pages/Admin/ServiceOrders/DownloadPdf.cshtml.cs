using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class DownloadPdfModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IServiceOrderPdfRenderer _pdf;

    public DownloadPdfModel(ApplicationDbContext db, IFileStorage storage, IServiceOrderPdfRenderer pdf)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, bool inline = false, bool refresh = false)
    {
        Response.Headers["Cache-Control"] = "no-store";

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();

        var fileName = $"OrdenServicio_{id:N}.pdf";

        // ✅ Si piden refresh o todavía no existe PDF, lo generamos con datos ACTUALES (incluye notas del checklist).
        if (refresh || string.IsNullOrWhiteSpace(o.PdfStoragePath))
        {
            var bytes = await _pdf.RenderAsync(o);
            var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{o.Id}/pdf", $"orden_{o.Id:N}.pdf", "application/pdf");
            o.PdfStoragePath = path;
            o.PdfGeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (inline)
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            else
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

            return File(bytes, "application/pdf");
        }

        var (stream, _, _) = await _storage.OpenAsync(o.PdfStoragePath, fileName);

        // En algunos entornos (proxy/cPanel) el streaming puede ser raro; devolver bytes es más robusto.
        await using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            if (inline)
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            else
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

            return File(bytes, "application/pdf");
        }
    }
}
