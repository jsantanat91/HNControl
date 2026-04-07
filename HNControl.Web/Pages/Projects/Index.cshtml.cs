using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;
    public IndexModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanEdit { get; set; }

    public record Row(Guid Id, string Title, string ClientName, string Responsible, DateTime StartDate, DateTime EstEnd, ProjectStatus Status, bool IsOverdue);
    public List<Row> Rows { get; set; } = new();
    public record ClientGroup(string ClientName, int Total, int Overdue, List<Row> Projects);
    public List<ClientGroup> Groups { get; set; } = new();

    public async Task OnGetAsync()
    {
        CanEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ProjectsEdit);
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var isAdmin = User.IsInRole(AppRoles.SuperAdmin);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var q = _db.Projects
            .AsNoTracking()
            .AsQueryable();

        if (!isAdmin && userId != null)
            q = q.Where(p => p.AssignedUserId == userId);

        if (DateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(DateFrom.Value.Date, DateTimeKind.Utc);
            q = q.Where(p => p.StartDate >= fromUtc);
        }

        if (DateTo.HasValue)
        {
            var toUtcExclusive = DateTime.SpecifyKind(DateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(p => p.StartDate < toUtcExclusive);
        }

        TotalCount = await q.CountAsync();

        var list = await q
            .OrderByDescending(p => p.StartDate)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.AssignedUserId,
                p.StartDate,
                p.EstimatedEndDate,
                p.Status,
                ClientName = p.Client != null ? p.Client.Name : "",
                ResponsibleName = p.AssignedEmployee != null ? p.AssignedEmployee.FullName : null
            })
            .ToListAsync();

        Rows = list.Select(p =>
        {
            var overdue = p.Status == ProjectStatus.Open && p.EstimatedEndDate.Date < DateTime.Today;
            return new Row(
                p.Id,
                p.Title,
                p.ClientName ?? "",
                p.ResponsibleName ?? p.AssignedUserId,
                p.StartDate,
                p.EstimatedEndDate,
                p.Status,
                overdue
            );
        }).ToList();

        Groups = Rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ClientName) ? "Sin cliente" : x.ClientName)
            .OrderBy(g => g.Key)
            .Select(g => new ClientGroup(
                g.Key,
                g.Count(),
                g.Count(x => x.IsOverdue),
                g.OrderByDescending(x => x.StartDate).ToList()))
            .ToList();
    }
}
