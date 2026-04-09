using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public DateOnly? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public ServiceOrderType? Type { get; set; }
    [BindProperty(SupportsGet = true)] public ServiceOrderStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    public bool IsSuperAdmin { get; private set; }
    [TempData] public string? Flash { get; set; }

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

    public record Row(
        Guid Id,
        string Title,
        string ClientName,
        string ProjectTitle,
        string ContractLabel,
        ServiceOrderType Type,
        ServiceOrderStatus Status,
        string Assigned,
        string ClaimedBy,
        DateTime? ClaimedAt,
        DateTime CreatedAt
    );

    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        IsSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        if (!DateFrom.HasValue && !DateTo.HasValue)
        {
            var now = DateOnly.FromDateTime(DateTime.Today);
            DateFrom = new DateOnly(now.Year, now.Month, 1);
            DateTo = DateFrom.Value.AddMonths(1).AddDays(-1);
        }

        var q = _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Project)
            .Include(o => o.ClientServiceContract)
            .Include(o => o.AssignedEmployee)
            .Include(o => o.ClaimedByEmployee)
            .AsQueryable();

        if (DateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(DateFrom.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(o => o.CreatedAt >= fromUtc);
        }

        if (DateTo.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            q = q.Where(o => o.CreatedAt < toExclusiveUtc);
        }

        if (Status.HasValue)
            q = q.Where(o => o.Status == Status.Value);
        if (Type.HasValue)
            q = q.Where(o => o.Type == Type.Value);

        TotalCount = await q.CountAsync();

        var list = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Rows = list.Select(o => new Row(
            o.Id,
            o.Title,
            o.Client?.Name ?? "-",
            o.Project?.Title ?? "-",
            o.ClientServiceContract?.Label ?? "-",
            o.Type,
            o.Status,
            o.AssignedEmployee?.FullName ?? "-",
            o.ClaimedByEmployee?.FullName ?? "-",
            o.ClaimedAt,
            o.CreatedAt
        )).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!User.IsInRole(AppRoles.SuperAdmin))
            return Forbid();

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (order is null)
        {
            Flash = "No se encontr\u00f3 la orden a eliminar.";
            return RedirectToPage("/Admin/ServiceOrders/Index", new
            {
                DateFrom = DateFrom?.ToString("yyyy-MM-dd"),
                DateTo = DateTo?.ToString("yyyy-MM-dd"),
                Type = Type?.ToString(),
                Status = Status?.ToString(),
                Page,
                PageSize
            });
        }

        _db.ServiceOrders.Remove(order);
        try
        {
            await _db.SaveChangesAsync();
            Flash = "Orden eliminada correctamente.";
        }
        catch (DbUpdateException)
        {
            Flash = "No se pudo eliminar la orden por dependencias relacionadas.";
        }

        return RedirectToPage("/Admin/ServiceOrders/Index", new
        {
            DateFrom = DateFrom?.ToString("yyyy-MM-dd"),
            DateTo = DateTo?.ToString("yyyy-MM-dd"),
            Type = Type?.ToString(),
            Status = Status?.ToString(),
            Page,
            PageSize
        });
    }
}
