using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class MyModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public MyModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public EmployeeProfile? Employee { get; set; }
    public SalesSellerProfile? Seller { get; set; }

    public record QuoteVm(Guid QuoteId, string Folio, string Customer, decimal Total, string Status, DateTime CreatedAt, bool HasPdf);
    public List<QuoteVm> Quotes { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return RedirectToPage("/Account/Login");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        Seller = await _db.SalesSellerProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeUserId == userId && x.IsActive);

        if (Seller == null)
            return RedirectToPage("/Employees/MyProfile");

        Quotes = await _db.SalesOpportunities
            .AsNoTracking()
            .Where(x => x.SellerProfileId == Seller.Id)
            .Include(x => x.QuoteRequest)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new QuoteVm(
                x.QuoteRequestId,
                x.QuoteRequest != null ? x.QuoteRequest.Folio : "-",
                x.QuoteRequest != null ? x.QuoteRequest.CustomerName : "-",
                x.QuoteRequest != null ? (x.QuoteRequest.EstimatedTotal ?? x.QuoteRequest.SubtotalAuto) : 0m,
                x.Status.ToString(),
                x.CreatedAt,
                x.QuoteRequest != null && !string.IsNullOrWhiteSpace(x.QuoteRequest.PdfStoragePath)))
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnGetDownloadQuotePdfAsync(Guid quoteId)
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var seller = await _db.SalesSellerProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeUserId == userId && x.IsActive);
        if (seller == null) return Forbid();

        var quote = await _db.SalesOpportunities
            .AsNoTracking()
            .Where(x => x.SellerProfileId == seller.Id && x.QuoteRequestId == quoteId)
            .Include(x => x.QuoteRequest)
            .Select(x => x.QuoteRequest)
            .FirstOrDefaultAsync();

        if (quote == null || string.IsNullOrWhiteSpace(quote.PdfStoragePath))
            return NotFound();

        var (stream, contentType, name) = await _storage.OpenAsync(quote.PdfStoragePath, $"{quote.Folio}.pdf");
        return File(stream, contentType, name);
    }
}
