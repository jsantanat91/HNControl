using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

[Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
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
    public DateTime WeekStart { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(DateTime? weekStart = null)
    {
        var userId = _userMgr.GetUserId(User)!;

        WeekStart = GetWeekStart(DateTime.UtcNow.Date);
        if (weekStart.HasValue) WeekStart = GetWeekStart(weekStart.Value.Date);

        Week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == WeekStart);

        if (Week == null)
        {
            Week = new ViaticWeek
            {
                UserId = userId,
                WeekStartDate = WeekStart,
                Status = ViaticWeekStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ViaticWeeks.Add(Week);
            await _db.SaveChangesAsync();
        }

        // Por si algo cambió, dejamos totales alineados
        await ViaticTotalsHelper.RecalcWeekAsync(_db, Week.Id);
        await _db.SaveChangesAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteEntryAsync(Guid entryId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var entry = await _db.ViaticEntries
            .Include(e => e.Week)
            .Include(e => e.Attachment)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry?.Week == null || entry.Week.UserId != userId) return NotFound();

        if (entry.Week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
        {
            Error = "Semana enviada/aprobada: no se puede modificar.";
            return RedirectToPage("/Viaticos/Week", new { weekStart = entry.Week.WeekStartDate.ToString("yyyy-MM-dd") });
        }

        if (entry.Attachment != null)
        {
            _db.ViaticAttachments.Remove(entry.Attachment);
        }

        _db.ViaticEntries.Remove(entry);
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, entry.WeekId);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { weekStart = entry.Week.WeekStartDate.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostToggleBillableAsync(Guid entryId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var entry = await _db.ViaticEntries
            .Include(e => e.Week)
            .Include(e => e.Attachment)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry?.Week == null || entry.Week.UserId != userId) return NotFound();

        if (entry.Week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
        {
            Error = "Semana enviada/aprobada: no se puede modificar.";
            return RedirectToPage("/Viaticos/Week", new { weekStart = entry.Week.WeekStartDate.ToString("yyyy-MM-dd") });
        }

        // Si lo quieres poner facturable, debe existir PDF
        if (!entry.IsBillable && entry.Attachment == null)
        {
            Error = "Para marcar facturable necesitas subir PDF (entra a editar/adjuntar).";
            return RedirectToPage("/Viaticos/Week", new { weekStart = entry.Week.WeekStartDate.ToString("yyyy-MM-dd") });
        }

        entry.IsBillable = !entry.IsBillable;
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, entry.WeekId);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { weekStart = entry.Week.WeekStartDate.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostSubmitWeekAsync(Guid weekId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == weekId && w.UserId == userId);

        if (week == null) return NotFound();

        if (week.Status is ViaticWeekStatus.Approved)
            return RedirectToPage("/Viaticos/Week", new { weekStart = week.WeekStartDate.ToString("yyyy-MM-dd") });

        // Regla: si hay entries billables sin PDF, no deja enviar
        var bad = week.Entries.Any(e => e.IsBillable && e.Attachment == null);
        if (bad)
        {
            Error = "Tienes gastos facturables sin PDF. Corrígelo antes de enviar.";
            return RedirectToPage("/Viaticos/Week", new { weekStart = week.WeekStartDate.ToString("yyyy-MM-dd") });
        }

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);

        week.Status = ViaticWeekStatus.Submitted;
        week.SubmittedAt = DateTime.UtcNow;
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Viaticos/Week", new { weekStart = week.WeekStartDate.ToString("yyyy-MM-dd") });
    }

    private static DateTime GetWeekStart(DateTime dateUtc)
    {
        var d = dateUtc.Date;
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }
}
