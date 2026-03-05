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

    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public string? Error { get; set; }

    public List<WeekRow> Weeks { get; set; } = new();

    public record WeekRow(Guid Id, DateTime WeekStartDate, ViaticWeekStatus Status, decimal Total);

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var q = _db.ViaticWeeks.Where(w => w.UserId == userId);

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

        TotalCount = await q.CountAsync();

        Weeks = await q
            .OrderByDescending(w => w.WeekStartDate)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
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
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }
}
