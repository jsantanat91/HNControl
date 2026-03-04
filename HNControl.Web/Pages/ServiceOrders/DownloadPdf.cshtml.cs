using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.ServiceOrders;

[Authorize]
public class DownloadPdfModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public DownloadPdfModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, bool inline = true)
    {
        if (User.IsInRole(AppRoles.Admin))
            return RedirectToPage("/Admin/ServiceOrders/DownloadPdf", new { id, inline, refresh = false });

        var order = await _db.ServiceOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (order == null) return NotFound();

        if (string.IsNullOrWhiteSpace(order.PdfStoragePath))
            return BadRequest("Esta orden aun no tiene PDF generado.");

        var downloadName = $"OrdenServicio_{id:N}.pdf";
        var (stream, contentType, _) = await _storage.OpenAsync(order.PdfStoragePath, downloadName);

        Response.Headers["Content-Disposition"] = inline
            ? $"inline; filename=\"{downloadName}\""
            : $"attachment; filename=\"{downloadName}\"";

        return File(stream, contentType, downloadName);
    }
}
