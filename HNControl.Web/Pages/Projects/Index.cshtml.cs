using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public record Row(Guid Id, string Title, string ClientName, string Responsible, DateTime StartDate, DateTime EstEnd, ProjectStatus Status, bool IsOverdue);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var isAdmin = User.IsInRole(AppRoles.Admin);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var q = _db.Projects
            .Include(p => p.Client)
            .Include(p => p.AssignedEmployee)
            .AsQueryable();

        if (!isAdmin && userId != null)
            q = q.Where(p => p.AssignedUserId == userId);

        if (DateFrom.HasValue)
        {
            var from = DateFrom.Value.Date;
            q = q.Where(p => p.StartDate.Date >= from);
        }

        if (DateTo.HasValue)
        {
            var to = DateTo.Value.Date;
            q = q.Where(p => p.StartDate.Date <= to);
        }

        TotalCount = await q.CountAsync();

        var list = await q
            .OrderByDescending(p => p.StartDate)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Rows = list.Select(p =>
        {
            var overdue = p.Status == ProjectStatus.Open && p.EstimatedEndDate.Date < DateTime.Today;
            return new Row(
                p.Id,
                p.Title,
                p.Client?.Name ?? "",
                p.AssignedEmployee?.FullName ?? p.AssignedUserId,
                p.StartDate,
                p.EstimatedEndDate,
                p.Status,
                overdue
            );
        }).ToList();
    }
}
