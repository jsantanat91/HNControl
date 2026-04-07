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
    public record ClientOrderGroup(string Client, int Total, int Overdue, List<Row> Orders);
    public List<ClientOrderGroup> RecentClientGroups { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.IsInRole(AppRoles.Admin))
            return Redirect("/Admin/ServiceOrders/Index");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        if (!DateFrom.HasValue && !DateTo.HasValue)
        {
            var nowLocal = DateTime.Now;
            DateFrom = new DateTime(nowLocal.Year, nowLocal.Month, 1);
            DateTo = nowLocal.Date;
        }

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

        var allRows = await q
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var normalized = allRows.Select(o =>
        {
            var closed = o.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed;
            var isMine = o.ClaimedByUserId == userId;
            var canTake = !closed && string.IsNullOrWhiteSpace(o.ClaimedByUserId);

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
                canTake,
                isMine,
                !string.IsNullOrWhiteSpace(o.PdfStoragePath)
            );
        }).ToList();

        var orderedClientNames = normalized
            .GroupBy(x => x.Client)
            .OrderByDescending(g => g.Max(x => x.CreatedAt))
            .Select(g => g.Key)
            .ToList();

        var recentClients = orderedClientNames.Take(3).ToHashSet(StringComparer.OrdinalIgnoreCase);

        RecentClientGroups = normalized
            .Where(x => recentClients.Contains(x.Client))
            .GroupBy(x => x.Client)
            .OrderByDescending(g => g.Max(x => x.CreatedAt))
            .Select(g => new ClientOrderGroup(
                g.Key,
                g.Count(),
                g.Count(IsOverdue),
                g.OrderByDescending(x => x.CreatedAt).Take(10).ToList()))
            .ToList();

        var overdueBacklog = normalized
            .Where(x => !recentClients.Contains(x.Client) && IsOverdue(x))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        TotalCount = overdueBacklog.Count;
        Rows = overdueBacklog
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

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

        if (!string.IsNullOrWhiteSpace(order.ClaimedByUserId) && order.ClaimedByUserId != userId)
        {
            TempData["Info"] = "La orden ya fue tomada por otro técnico. Pide al admin desasignarla.";
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

    private static bool IsOverdue(Row row)
    {
        if (string.IsNullOrWhiteSpace(row.Due) || row.Due == "-")
            return false;

        if (!DateTime.TryParse(row.Due, out var due))
            return false;

        var isClosed = row.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed;
        return !isClosed && due.Date < DateTime.Today;
    }
}
