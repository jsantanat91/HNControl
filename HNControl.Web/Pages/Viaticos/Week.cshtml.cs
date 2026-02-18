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

    [TempData] public string? Error { get; set; }
    [TempData] public string? Info { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id = null, DateTime? weekStart = null)
    {
        var userId = _userMgr.GetUserId(User)!;

        // ✅ Si viene por ID, cargamos esa semana (evita que te mande “a la semana actual” y parezca que jaló datos viejos).
        if (id.HasValue)
        {
            Week = await _db.ViaticWeeks
                .Include(w => w.Entries).ThenInclude(e => e.Attachment)
                .FirstOrDefaultAsync(w => w.Id == id.Value && w.UserId == userId);

            if (Week == null) return NotFound();
            WeekStart = Week.WeekStartDate.Date;

            await ViaticTotalsHelper.RecalcWeekAsync(_db, Week.Id);
            await _db.SaveChangesAsync();
            return Page();
        }

        // ✅ Fallback: por semana (fecha) o semana actual
        WeekStart = GetWeekStart((weekStart ?? DateTime.UtcNow.Date).Date);

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

        await ViaticTotalsHelper.RecalcWeekAsync(_db, Week.Id);
        await _db.SaveChangesAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostClearWeekAsync(Guid weekId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == weekId && w.UserId == userId);

        if (week == null) return NotFound();

        if (week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
        {
            Error = "Semana enviada/aprobada: no se puede limpiar.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        // Borramos entradas (attachments se van por cascade, pero las removemos explícito por claridad)
        foreach (var e in week.Entries.ToList())
        {
            if (e.Attachment != null)
                _db.ViaticAttachments.Remove(e.Attachment);

            _db.ViaticEntries.Remove(e);
        }

        week.TotalAmount = 0m;
        week.BillableAmount = 0m;
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        Info = "Semana limpiada. Ahora sí: hoja en blanco.";
        return RedirectToPage("/Viaticos/Week", new { id = weekId });
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
            return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
        }

        if (entry.Attachment != null)
            _db.ViaticAttachments.Remove(entry.Attachment);

        _db.ViaticEntries.Remove(entry);
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, entry.WeekId);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
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
            return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
        }

        // Si lo quieres poner facturable, debe existir PDF
        if (!entry.IsBillable && entry.Attachment == null)
        {
            Error = "Para marcar facturable necesitas subir PDF (usa el botón Editar).";
            return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
        }

        entry.IsBillable = !entry.IsBillable;
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, entry.WeekId);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
    }

    public async Task<IActionResult> OnPostSubmitWeekAsync(Guid weekId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == weekId && w.UserId == userId);

        if (week == null) return NotFound();

        if (week.Status is ViaticWeekStatus.Approved)
            return RedirectToPage("/Viaticos/Week", new { id = weekId });

        // Regla: si hay entries billables sin PDF, no deja enviar
        var bad = week.Entries.Any(e => e.IsBillable && e.Attachment == null);
        if (bad)
        {
            Error = "Tienes gastos facturables sin PDF. Corrígelo antes de enviar.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);

        week.Status = ViaticWeekStatus.Submitted;
        week.SubmittedAt = DateTime.UtcNow;
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = "Semana enviada al admin para revisión.";
        return RedirectToPage("/Viaticos/Week", new { id = weekId });
    }

    private static DateTime GetWeekStart(DateTime dateUtc)
    {
        var d = dateUtc.Date;
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }
}
