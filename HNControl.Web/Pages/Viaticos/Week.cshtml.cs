using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

public class WeekModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public WeekModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    public ViaticWeek? Week { get; set; }
    public decimal WeekTotal { get; set; }

    public List<DayBlock> Days { get; set; } = new();

    public record EntryRow(Guid Id, ViaticCategory Category, string Description, decimal Amount, bool IsBillable, Guid? AttachmentId);
    public record DayBlock(DateTime Date, List<EntryRow> Entries, decimal Total);

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _userMgr.GetUserId(User)!;

        Week = await _db.ViaticWeeks
            .Include(w => w.Entries)
                .ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (Week == null) return NotFound();

        var start = Week.WeekStartDate.Date;
        var allDays = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();

        foreach (var d in allDays)
        {
            var entries = Week.Entries
                .Where(e => e.DayDate.Date == d.Date)
                .OrderBy(e => e.Category)
                .Select(e => new EntryRow(e.Id, e.Category, e.Description, e.Amount, e.IsBillable, e.Attachment?.Id))
                .ToList();

            Days.Add(new DayBlock(d, entries, entries.Sum(x => x.Amount)));
        }

        WeekTotal = Week.Entries.Sum(e => e.Amount);
        return Page();
    }
}
