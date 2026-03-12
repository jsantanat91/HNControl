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
    private readonly IFileStorage _storage;

    public WeekModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public ViaticWeek? Week { get; set; }
    public DateTime WeekStart { get; set; }

    [TempData] public string? Error { get; set; }
    [TempData] public string? Info { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id = null, DateTime? weekStart = null)
    {
        var userId = _userMgr.GetUserId(User)!;

        if (id.HasValue)
        {
            Week = await _db.ViaticWeeks
                .Include(w => w.RelatedServiceOrder)
                .Include(w => w.Entries).ThenInclude(e => e.Attachment)
                .FirstOrDefaultAsync(w => w.Id == id.Value && w.UserId == userId);

            if (Week == null) return NotFound();
            WeekStart = Week.WeekStartDate.Date;

            await ViaticTotalsHelper.RecalcWeekAsync(_db, Week.Id);
            await _db.SaveChangesAsync();
            return Page();
        }

        WeekStart = GetWeekStart((weekStart ?? DateTime.UtcNow.Date).Date);

        Week = await _db.ViaticWeeks
            .Include(w => w.RelatedServiceOrder)
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.FlowType == ViaticFlowType.Weekly && w.WeekStartDate == WeekStart);

        if (Week == null)
        {
            Week = new ViaticWeek
            {
                UserId = userId,
                WeekStartDate = WeekStart,
                FlowType = ViaticFlowType.Weekly,
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
        if (!CanEditWeek(week))
        {
            Error = "La semana ya fue enviada o cerrada; no se puede limpiar.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        foreach (var e in week.Entries.ToList())
        {
            if (e.Attachment != null)
            {
                await _storage.DeleteIfExistsAsync(e.Attachment.StoragePath);
                _db.ViaticAttachments.Remove(e.Attachment);
            }

            _db.ViaticEntries.Remove(e);
        }

        week.TotalAmount = 0m;
        week.BillableAmount = 0m;
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        Info = "Semana limpiada.";
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
        if (!CanEditWeek(entry.Week))
        {
            Error = "La semana ya fue enviada o cerrada; no se puede modificar.";
            return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
        }

        if (entry.Attachment != null)
        {
            await _storage.DeleteIfExistsAsync(entry.Attachment.StoragePath);
            _db.ViaticAttachments.Remove(entry.Attachment);
        }

        _db.ViaticEntries.Remove(entry);
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, entry.WeekId);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
    }

    public async Task<IActionResult> OnPostDeleteWeekAsync(Guid weekId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == weekId && w.UserId == userId);

        if (week == null) return NotFound();

        if (week.Status != ViaticWeekStatus.Draft && week.Status != ViaticWeekStatus.Rejected)
        {
            Error = "Solo puedes eliminar semanas en estado borrador o rechazado.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        foreach (var e in week.Entries)
        {
            if (e.Attachment != null)
                await _storage.DeleteIfExistsAsync(e.Attachment.StoragePath);
        }

        _db.ViaticWeeks.Remove(week);
        await _db.SaveChangesAsync();

        Info = "Semana eliminada.";
        return RedirectToPage("/Viaticos/Index");
    }

    public async Task<IActionResult> OnPostToggleBillableAsync(Guid entryId)
    {
        var userId = _userMgr.GetUserId(User)!;

        var entry = await _db.ViaticEntries
            .Include(e => e.Week)
            .Include(e => e.Attachment)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry?.Week == null || entry.Week.UserId != userId) return NotFound();
        if (!CanEditWeek(entry.Week))
        {
            Error = "La semana ya fue enviada o cerrada; no se puede modificar.";
            return RedirectToPage("/Viaticos/Week", new { id = entry.WeekId });
        }

        if (!entry.IsBillable && entry.Attachment == null)
        {
            Error = "Para marcar facturable necesitas adjuntar evidencia (PDF o imagen).";
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

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);

        if (week.FlowType == ViaticFlowType.Weekly)
        {
            if (week.Status == ViaticWeekStatus.Approved)
                return RedirectToPage("/Viaticos/Week", new { id = weekId });

            var badWeekly = week.Entries.Any(e => e.IsBillable && e.Attachment == null);
            if (badWeekly)
            {
                Error = "Tienes gastos facturables sin comprobante. Corrige antes de enviar.";
                return RedirectToPage("/Viaticos/Week", new { id = weekId });
            }

            week.Status = ViaticWeekStatus.Submitted;
            week.SubmittedAt = DateTime.UtcNow;
            week.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            Info = "Semana enviada al admin para revisión.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        // Flujo anticipado de viaje
        if (week.Status is ViaticWeekStatus.Draft or ViaticWeekStatus.Rejected)
        {
            if (week.RequestedAdvanceAmount <= 0m || string.IsNullOrWhiteSpace(week.TripDestination) || string.IsNullOrWhiteSpace(week.TripPurpose))
            {
                Error = "Completa destino, motivo y monto solicitado antes de enviar la solicitud.";
                return RedirectToPage("/Viaticos/Week", new { id = weekId });
            }

            week.Status = ViaticWeekStatus.Submitted;
            week.SubmittedAt = DateTime.UtcNow;
            week.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            Info = "Solicitud anticipada enviada al admin para autorización.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        if (week.Status == ViaticWeekStatus.Approved)
        {
            if (!week.Entries.Any())
            {
                Error = "Agrega al menos un gasto comprobado para enviar a revisión final.";
                return RedirectToPage("/Viaticos/Week", new { id = weekId });
            }
            week.Status = ViaticWeekStatus.SettlementSubmitted;
            week.SettlementSubmittedAt = DateTime.UtcNow;
            week.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            Info = "Comprobación enviada a revisión final del admin.";
            return RedirectToPage("/Viaticos/Week", new { id = weekId });
        }

        Error = "Esta semana no está en estado editable para enviar.";
        return RedirectToPage("/Viaticos/Week", new { id = weekId });
    }

    private static bool CanEditWeek(ViaticWeek week)
    {
        if (week.FlowType == ViaticFlowType.Weekly)
            return week.Status is ViaticWeekStatus.Draft or ViaticWeekStatus.Rejected;

        return week.Status is ViaticWeekStatus.Draft or ViaticWeekStatus.Rejected or ViaticWeekStatus.Approved;
    }

    private static DateTime GetWeekStart(DateTime dateUtc)
    {
        var d = dateUtc.Date;
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }
}

