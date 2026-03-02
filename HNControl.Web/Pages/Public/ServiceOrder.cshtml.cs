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

    public ServiceOrderModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public string Token { get; set; } = "";
    public Models.ServiceOrder? Order { get; set; }
    public bool CanDownloadPdf => Order != null && !string.IsNullOrWhiteSpace(Order.PdfStoragePath);

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

        if (string.IsNullOrWhiteSpace(Order.PdfStoragePath))
            return BadRequest("Aún no hay PDF.");

        var (stream, contentType, originalName) = await _storage.OpenAsync(Order.PdfStoragePath, $"orden_{Order.Id:N}.pdf");
        return File(stream, contentType, originalName);
    }
}
