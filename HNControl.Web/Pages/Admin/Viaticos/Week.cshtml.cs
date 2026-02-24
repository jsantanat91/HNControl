using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Viaticos;

[Authorize(Roles = AppRoles.Admin)]
public class WeekModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public WeekModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public ViaticWeek? Week { get; set; }
    [TempData] public string? Error { get; set; }
    [TempData] public string? Info { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Week = await _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (Week == null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var adminId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (week == null) return NotFound();

        if (week.Status != ViaticWeekStatus.Submitted)
        {
            Error = "Solo puedes aprobar semanas en estado Submitted.";
            return RedirectToPage(new { id });
        }

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);

        week.Status = ViaticWeekStatus.Approved;
        week.ApprovedAt = DateTime.UtcNow;
        week.ApprovedByUserId = adminId;
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = "Semana aprobada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string reason)
    {
        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == id);
        if (week == null) return NotFound();

        if (week.Status != ViaticWeekStatus.Submitted)
        {
            Error = "Solo puedes rechazar semanas en estado Submitted.";
            return RedirectToPage(new { id });
        }

        week.Status = ViaticWeekStatus.Rejected;
        week.AdminNotes = (reason ?? "").Trim();
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = "Semana rechazada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (week == null) return NotFound();

        foreach (var e in week.Entries)
        {
            if (e.Attachment != null)
                await _storage.DeleteIfExistsAsync(e.Attachment.StoragePath);
        }

        _db.ViaticWeeks.Remove(week);
        await _db.SaveChangesAsync();

        Info = "Semana eliminada.";
        return RedirectToPage("/Admin/Viaticos/Index", new { userId = week.UserId });
    }
}
