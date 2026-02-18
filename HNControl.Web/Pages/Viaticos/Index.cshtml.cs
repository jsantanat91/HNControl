using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

[Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    [BindProperty]
    public DateTime AnyDayInWeek { get; set; } = DateTime.Today;

    public string? Error { get; set; }

    public List<WeekRow> Weeks { get; set; } = new();

    public record WeekRow(Guid Id, DateTime WeekStartDate, ViaticWeekStatus Status, decimal Total);

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        Weeks = await _db.ViaticWeeks
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.WeekStartDate)
            .Select(w => new WeekRow(
                w.Id,
                w.WeekStartDate,
                w.Status,
                w.Entries.Sum(e => e.Amount)
            ))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        var monday = ToMonday(AnyDayInWeek);
        var exists = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == monday);

        if (exists == null)
        {
            exists = new ViaticWeek
            {
                UserId = userId,
                WeekStartDate = monday,
                Status = ViaticWeekStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ViaticWeeks.Add(exists);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage("/Viaticos/Week", new { id = exists.Id });
    }

    private static DateTime ToMonday(DateTime anyDay)
    {
        var d = anyDay.Date;
        // En .NET: Sunday=0 ... Saturday=6
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }
}
