using HNControl.Web.Data;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

[AllowAnonymous]
public class QuoteSuccessModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public QuoteSuccessModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Folio { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int Sent { get; set; }

    public bool SentOk => Sent == 1;
    public bool HasPdf { get; set; }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Folio))
            Folio = "Sin folio";

        if (Id.HasValue)
        {
            HasPdf = await _db.QuoteRequests.AsNoTracking()
                .AnyAsync(x => x.Id == Id.Value && !string.IsNullOrWhiteSpace(x.PdfStoragePath));
        }
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid id)
    {
        var req = await _db.QuoteRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (req == null || string.IsNullOrWhiteSpace(req.PdfStoragePath))
            return NotFound();

        var (stream, contentType, name) = await _storage.OpenAsync(req.PdfStoragePath, $"{req.Folio}.pdf");
        return File(stream, contentType, name);
    }
}

