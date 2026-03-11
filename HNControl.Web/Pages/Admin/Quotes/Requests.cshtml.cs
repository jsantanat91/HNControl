using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Quotes;

[Authorize(Roles = AppRoles.Admin)]
public class RequestsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public RequestsModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true, Name = "segment")]
    public string? Segment { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public string? To { get; set; }

    [TempData]
    public string? Message { get; set; }

    public List<RowVm> Rows { get; set; } = [];

    public async Task OnGetAsync()
    {
        var query = _db.QuoteRequests
            .AsNoTracking()
            .Include(x => x.Lines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim().ToLower();
            query = query.Where(x =>
                x.Folio.ToLower().Contains(q) ||
                x.CustomerName.ToLower().Contains(q) ||
                x.CustomerEmail.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(Segment) && Enum.TryParse<QuoteSegment>(Segment, out var seg))
            query = query.Where(x => x.Segment == seg);

        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<QuoteRequestStatus>(Status, out var st))
            query = query.Where(x => x.Status == st);

        if (DateOnly.TryParse(From, out var fromDate))
            query = query.Where(x => x.CreatedAt >= fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (DateOnly.TryParse(To, out var toDate))
            query = query.Where(x => x.CreatedAt < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        Rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new RowVm
            {
                Id = x.Id,
                Folio = x.Folio,
                CustomerName = x.CustomerName,
                CustomerEmail = x.CustomerEmail,
                CustomerPhone = x.CustomerPhone,
                CustomerLocation = x.CustomerLocation,
                SegmentLabel = x.Segment == QuoteSegment.Business ? "Empresarial" : "Residencial",
                Status = x.Status.ToString(),
                CreatedAtLocal = x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                AcceptedAtLocal = x.AcceptedAt.HasValue ? x.AcceptedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : null,
                EstimatedTotal = x.EstimatedTotal ?? x.SubtotalAuto,
                ManualItemsCount = x.ManualItemsCount,
                HasPdf = !string.IsNullOrWhiteSpace(x.PdfStoragePath),
                Lines = x.Lines.Select(l => new LineVm
                {
                    CategoryName = l.CategoryName,
                    ServiceName = l.ServiceName,
                    SubproductName = l.SubproductName,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    PriceIncludesVat = l.PriceIncludesVat,
                    IsManualPrice = l.IsManualPrice,
                    LineTotal = l.LineTotal,
                    OfferType = l.OfferType.ToString()
                }).ToList()
            })
            .ToListAsync();

        foreach (var r in Rows)
            r.StatusLabel = LabelStatus(r.Status);
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        var req = await _db.QuoteRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null)
            return RedirectToPage(new { q = Q, segment = Segment, status = Status, from = From, to = To });

        req.Status = QuoteRequestStatus.Accepted;
        req.AcceptedAt = DateTime.UtcNow;
        req.AcceptedByUserId = User.Identity?.Name;
        await _db.SaveChangesAsync();

        Message = $"Cotizacion {req.Folio} marcada como Aceptada.";
        return RedirectToPage(new { q = Q, segment = Segment, status = Status, from = From, to = To });
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(Guid id)
    {
        var req = await _db.QuoteRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (req == null || string.IsNullOrWhiteSpace(req.PdfStoragePath))
            return NotFound();

        var (stream, contentType, name) = await _storage.OpenAsync(req.PdfStoragePath, $"{req.Folio}.pdf");
        return File(stream, contentType, name);
    }

    public class RowVm
    {
        public Guid Id { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerLocation { get; set; } = string.Empty;
        public string SegmentLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string CreatedAtLocal { get; set; } = string.Empty;
        public string? AcceptedAtLocal { get; set; }
        public decimal EstimatedTotal { get; set; }
        public int ManualItemsCount { get; set; }
        public bool HasPdf { get; set; }
        public List<LineVm> Lines { get; set; } = [];
    }

    public class LineVm
    {
        public string CategoryName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string? SubproductName { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool PriceIncludesVat { get; set; }
        public bool IsManualPrice { get; set; }
        public decimal? LineTotal { get; set; }
        public string OfferType { get; set; } = string.Empty;
    }

    public static string LabelStatus(string status) => status switch
    {
        nameof(QuoteRequestStatus.New) => "Nueva",
        nameof(QuoteRequestStatus.Emailed) => "Enviada",
        nameof(QuoteRequestStatus.EmailError) => "Error envio",
        nameof(QuoteRequestStatus.Accepted) => "Aceptada",
        _ => status
    };
}
