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

    public List<Row> Rows { get; set; } = new();

    public record Row(Guid WeekId, string EmployeeName, DateTime WeekStart, decimal Total, ViaticWeekStatus Status);

    public async Task OnGetAsync()
    {
        var q = _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.Entries)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(UserId))
            q = q.Where(w => w.UserId == UserId);

        Rows = await q
            .OrderByDescending(w => w.WeekStartDate)
            .Select(w => new Row(
                w.Id,
                w.EmployeeProfile != null ? w.EmployeeProfile.FullName : w.UserId,
                w.WeekStartDate,
                w.Entries.Sum(e => e.Amount),
                w.Status
            ))
            .ToListAsync();
    }
}
