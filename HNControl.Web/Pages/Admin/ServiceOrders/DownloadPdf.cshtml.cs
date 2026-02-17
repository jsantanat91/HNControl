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

    public DownloadPdfModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null || string.IsNullOrWhiteSpace(o.PdfStoragePath)) return NotFound();

        var (stream, contentType, downloadName) = await _storage.OpenAsync(o.PdfStoragePath, $"OrdenServicio_{id:N}.pdf");
        return File(stream, contentType, downloadName);
    }
}
