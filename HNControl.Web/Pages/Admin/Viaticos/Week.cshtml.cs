using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Viaticos;

public class WeekModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public WeekModel(ApplicationDbContext db) { _db = db; }

    public ViaticWeek? Week { get; set; }
    public string EmployeeName { get; set; } = "";
    public decimal WeekTotal { get; set; }

    public string? Info { get; set; }

    public bool CanApprove => Week != null && Week.Status != ViaticWeekStatus.Approved;
    public bool CanReject => Week != null && Week.Status != ViaticWeekStatus.Rejected;

    public List<DayBlock> Days { get; set; } = new();

    public record EntryRow(Guid Id, ViaticCategory Category, string Description, decimal Amount, bool IsBillable, Guid? AttachmentId);
    public record DayBlock(DateTime Date, List<EntryRow> Entries, decimal Total);

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadWeekAsync(id);
        if (Week == null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == id);
        if (week == null) return NotFound();

        week.Status = ViaticWeekStatus.Approved;
        week.ApprovedAt = DateTime.UtcNow;
        week.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        Info = "Semana aprobada.";
        await LoadWeekAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == id);
        if (week == null) return NotFound();

        week.Status = ViaticWeekStatus.Rejected;
        week.ApprovedAt = null;
        week.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        Info = "Semana rechazada.";
        await LoadWeekAsync(id);
        return Page();
    }

    private async Task LoadWeekAsync(Guid id)
    {
        Week = await _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.Entries)
                .ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        Days.Clear();

        if (Week == null) return;

        EmployeeName = Week.EmployeeProfile?.FullName ?? Week.UserId;

        var start = Week.WeekStartDate.Date;
        var allDays = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();

        foreach (var d in allDays)
        {
            var entries = Week.Entries
                .Where(e => e.DayDate.Date == d.Date)
                .OrderBy(e => e.Category)
                .Select(e => new EntryRow(
                    e.Id,
                    e.Category,
                    e.Description,
                    e.Amount,
                    e.IsBillable,
                    e.Attachment?.Id
                ))
                .ToList();

            Days.Add(new DayBlock(d, entries, entries.Sum(x => x.Amount)));
        }

        WeekTotal = Week.Entries.Sum(e => e.Amount);
    }
}
