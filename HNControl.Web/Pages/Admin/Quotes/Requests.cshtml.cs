using HNControl.Web.Data;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Quotes;

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

    [BindProperty(SupportsGet = true, Name = "from")]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public string? To { get; set; }

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

        if (!string.IsNullOrWhiteSpace(Segment) && Enum.TryParse<HNControl.Web.Models.QuoteSegment>(Segment, out var seg))
            query = query.Where(x => x.Segment == seg);

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
                SegmentLabel = x.Segment == HNControl.Web.Models.QuoteSegment.Business ? "Empresarial" : "Residencial",
                CreatedAtLocal = x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
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
                    LineTotal = l.LineTotal
                }).ToList()
            })
            .ToListAsync();
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
        public string CreatedAtLocal { get; set; } = string.Empty;
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
    }
}
