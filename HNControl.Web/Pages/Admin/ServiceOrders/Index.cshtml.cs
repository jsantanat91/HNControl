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
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var q = _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Project)
            .Include(o => o.ClientServiceContract)
            .Include(o => o.AssignedEmployee)
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
}
