using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Viaticos;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) { _db = db; }

    [BindProperty(SupportsGet = true)] public string? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    /// <summary>"pendientes" (default), "todos" or a <see cref="ViaticWeekStatus"/> name.</summary>
    [BindProperty(SupportsGet = true)] public string? Estatus { get; set; }

    public const string PendingFilter = "pendientes", AllFilter = "todos";
    /// <summary>Closed weeks hidden by default: approved (advance or settlement) and rejected.</summary>
    public static readonly ViaticWeekStatus[] ClosedStatuses =
        [ViaticWeekStatus.Approved, ViaticWeekStatus.SettlementApproved, ViaticWeekStatus.Rejected];
    public bool OnlyPending { get; private set; }
    public ViaticWeekStatus? StatusFilter { get; private set; }
    public string EstatusKey => OnlyPending ? PendingFilter : StatusFilter?.ToString() ?? AllFilter;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public List<Row> Rows { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public List<EmployeeAggregate> GlobalByEmployee { get; set; } = new();

    public record Row(Guid WeekId, string UserId, string EmployeeName, DateTime WeekStart, decimal Total, ViaticWeekStatus Status);
    public record Group(string UserId, string EmployeeName, List<Row> Weeks, decimal PageTotalAmount, decimal GlobalTotalAmount, int GlobalWeeks);
    public record EmployeeAggregate(string UserId, string EmployeeName, decimal TotalAmount, int Weeks);

    /// <summary>Parses Estatus: empty or "pendientes" → only pending; "todos" → all; otherwise a status name.</summary>
    public static (bool OnlyPending, ViaticWeekStatus? Status) ParseEstatus(string? value)
    {
        var key = (value ?? "").Trim();
        if (key.Length == 0 || key.Equals(PendingFilter, StringComparison.OrdinalIgnoreCase)) return (true, null);
        if (!key.Equals(AllFilter, StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<ViaticWeekStatus>(key, true, out var parsed) && Enum.IsDefined(parsed)) return (false, parsed);
        return (false, null);
    }

    public static IQueryable<ViaticWeek> ApplyStatusFilter(IQueryable<ViaticWeek> q, bool onlyPending, ViaticWeekStatus? status) =>
        onlyPending ? q.Where(w => !ClosedStatuses.Contains(w.Status))
        : status is { } s ? q.Where(w => w.Status == s)
        : q;

    public async Task OnGetAsync()
    {
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        (OnlyPending, StatusFilter) = ParseEstatus(Estatus);

        // Si no filtran rango, se toma mes actual por defecto; los pendientes se muestran de cualquier fecha.
        if (!OnlyPending && !DateFrom.HasValue && !DateTo.HasValue)
        {
            var now = DateTime.Now.Date;
            DateFrom = new DateTime(now.Year, now.Month, 1);
            DateTo = DateFrom.Value.AddMonths(1).AddDays(-1);
        }

        var q = _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.Entries)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(UserId))
            q = q.Where(w => w.UserId == UserId);

        q = ApplyStatusFilter(q, OnlyPending, StatusFilter);

        if (DateFrom.HasValue)
        {
            var from = DateFrom.Value.Date;
            q = q.Where(w => w.WeekStartDate.Date >= from);
        }

        if (DateTo.HasValue)
        {
            var to = DateTo.Value.Date;
            q = q.Where(w => w.WeekStartDate.Date <= to);
        }

        GlobalByEmployee = await q
            .GroupBy(w => new { w.UserId, EmployeeName = w.EmployeeProfile != null ? w.EmployeeProfile.FullName : w.UserId })
            .Select(g => new EmployeeAggregate(
                g.Key.UserId,
                g.Key.EmployeeName,
                g.Sum(x => x.Entries.Sum(e => e.Amount)),
                g.Count()
            ))
            .ToListAsync();

        var globalMap = GlobalByEmployee.ToDictionary(x => x.UserId, x => x);

        TotalCount = await q.CountAsync();

        Rows = await q
            .OrderByDescending(w => w.WeekStartDate)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .Select(w => new Row(
                w.Id,
                w.UserId,
                w.EmployeeProfile != null ? w.EmployeeProfile.FullName : w.UserId,
                w.WeekStartDate,
                w.Entries.Sum(e => e.Amount),
                w.Status
            ))
            .ToListAsync();

        Groups = Rows
            .GroupBy(r => new { r.UserId, r.EmployeeName })
            .OrderBy(g => g.Key.EmployeeName)
            .Select(g => new Group(
                g.Key.UserId,
                g.Key.EmployeeName,
                g.OrderByDescending(x => x.WeekStart).ToList(),
                g.Sum(x => x.Total),
                globalMap.TryGetValue(g.Key.UserId, out var agg) ? agg.TotalAmount : g.Sum(x => x.Total),
                globalMap.TryGetValue(g.Key.UserId, out var aggWeeks) ? aggWeeks.Weeks : g.Count()
            ))
            .ToList();
    }
}
