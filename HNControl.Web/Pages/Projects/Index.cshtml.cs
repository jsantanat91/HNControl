using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(Guid Id, string Title, string ClientName, string Responsible, string StartDate, string EstEnd, string Status, bool IsOverdue);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var q = _db.Projects
            .Include(p => p.Client)
            .Include(p => p.AssignedEmployee)
            .AsQueryable();

        if (!isAdmin && userId != null)
            q = q.Where(p => p.AssignedUserId == userId);

        var list = await q.OrderByDescending(p => p.StartDate).ToListAsync();

        Rows = list.Select(p =>
        {
            var overdue = p.Status == ProjectStatus.Open && p.EstimatedEndDate.Date < DateTime.Today;
            return new Row(
                p.Id,
                p.Title,
                p.Client?.Name ?? "",
                p.AssignedEmployee?.FullName ?? p.AssignedUserId,
                p.StartDate.ToString("yyyy-MM-dd"),
                p.EstimatedEndDate.ToString("yyyy-MM-dd"),
                p.Status.ToString(),
                overdue
            );
        }).ToList();
    }
}
