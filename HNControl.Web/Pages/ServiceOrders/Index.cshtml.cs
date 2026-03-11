using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.ServiceOrders;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public ServiceOrderType? Type { get; set; }
    [BindProperty(SupportsGet = true)] public ServiceOrderStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public List<ServiceOrderStatus> StatusOptions { get; } = new()
    {
        ServiceOrderStatus.Created,
        ServiceOrderStatus.InProgress,
        ServiceOrderStatus.InReview,
        ServiceOrderStatus.Finalized,
        ServiceOrderStatus.Rejected
    };
    public List<ServiceOrderType> TypeOptions { get; } = new()
    {
        ServiceOrderType.Correctivo,
        ServiceOrderType.Preventivo,
        ServiceOrderType.NuevaInstalacion,
        ServiceOrderType.LevantamientoTecnico,
        ServiceOrderType.Eventos,
        ServiceOrderType.Global
    };

    public string? Info { get; set; }

    public record Row(
        Guid Id,
        string Client,
        string Title,
        ServiceOrderType Type,
        ServiceOrderStatus Status,
        ServiceOrderWorkflowArea Area,
        string ClaimedBy,
        DateTime CreatedAt,
        string Due,
        bool CanTake,
        bool IsMine,
        bool HasPdf);

    public List<Row> Rows { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.IsInRole(AppRoles.Admin))
            return Redirect("/Admin/ServiceOrders/Index");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var q = _db.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Include(o => o.ClaimedByEmployee)
            .AsQueryable();

        if (DateFrom.HasValue)
        {
            var from = DateFrom.Value.Date;
            q = q.Where(o => o.CreatedAt.Date >= from);
        }

        if (DateTo.HasValue)
        {
            var to = DateTo.Value.Date;
            q = q.Where(o => o.CreatedAt.Date <= to);
        }

        if (Status.HasValue)
            q = q.Where(o => o.Status == Status.Value);
        if (Type.HasValue)
            q = q.Where(o => o.Type == Type.Value);

        TotalCount = await q.CountAsync();

        var orders = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Rows = orders.Select(o =>
        {
            var closed = o.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed;
            var isMine = o.ClaimedByUserId == userId;

            return new Row(
                o.Id,
                o.Client?.Name ?? "-",
                o.Title,
                o.Type,
                o.Status,
                o.CurrentArea,
                o.ClaimedByEmployee?.FullName ?? "Sin tomar",
                o.CreatedAt,
                o.EstimatedEndDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-",
                !closed,
                isMine,
                !string.IsNullOrWhiteSpace(o.PdfStoragePath)
            );
        }).ToList();

        Info = TempData["Info"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostTakeAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        if (order.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
        {
            TempData["Info"] = "La orden ya no acepta edicion.";
            return RedirectToPage();
        }

        order.ClaimedByUserId = userId;
        order.ClaimedAt = DateTime.UtcNow;

        if (order.Status == ServiceOrderStatus.Created)
        {
            order.Status = ServiceOrderStatus.InProgress;
            order.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        TempData["Info"] = "Orden tomada. Ya puedes editarla.";
        return RedirectToPage("/ServiceOrders/Work", new { id = order.Id });
    }
}
