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
    public decimal SettlementDifference { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Week = await _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.RelatedServiceOrder)
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (Week == null) return NotFound();
        SettlementDifference = Week.TotalAmount - Week.ApprovedAdvanceAmount;
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, decimal? approvedAdvanceAmount)
    {
        var adminId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (week == null) return NotFound();

        if (week.Status != ViaticWeekStatus.Submitted)
        {
            Error = "Solo puedes aprobar semanas en estado Enviado.";
            return RedirectToPage(new { id });
        }

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);

        week.Status = ViaticWeekStatus.Approved;
        week.ApprovedAt = DateTime.UtcNow;
        week.ApprovedByUserId = adminId;
        week.UpdatedAt = DateTime.UtcNow;

        if (week.FlowType == ViaticFlowType.TravelAdvance)
        {
            var finalApproved = approvedAdvanceAmount.GetValueOrDefault();
            if (finalApproved <= 0m)
                finalApproved = week.RequestedAdvanceAmount;

            week.ApprovedAdvanceAmount = finalApproved;
            week.DepositedAt = DateTime.UtcNow;
            week.DepositedByUserId = adminId;
        }

        await _db.SaveChangesAsync();
        Info = week.FlowType == ViaticFlowType.TravelAdvance
            ? "Solicitud de viaje aprobada y marcada como depositada."
            : "Semana aprobada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveSettlementAsync(Guid id)
    {
        var adminId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (week == null) return NotFound();

        if (week.FlowType != ViaticFlowType.TravelAdvance || week.Status != ViaticWeekStatus.SettlementSubmitted)
        {
            Error = "Solo aplica para comprobaciones enviadas de viaje anticipado.";
            return RedirectToPage(new { id });
        }

        week.Status = ViaticWeekStatus.SettlementApproved;
        week.SettlementApprovedAt = DateTime.UtcNow;
        week.SettlementApprovedByUserId = adminId;
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = "Comprobación final aprobada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string reason)
    {
        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == id);
        if (week == null) return NotFound();

        if (week.Status is not ViaticWeekStatus.Submitted and not ViaticWeekStatus.SettlementSubmitted)
        {
            Error = "Solo puedes rechazar semanas en estado Enviado o Comprobación enviada.";
            return RedirectToPage(new { id });
        }

        week.Status = ViaticWeekStatus.Rejected;
        week.AdminNotes = (reason ?? "").Trim();
        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = "Semana rechazada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateDifferenceAdjustmentAsync(Guid id, string mode)
    {
        var week = await _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (week == null) return NotFound();

        if (week.FlowType != ViaticFlowType.TravelAdvance || week.Status != ViaticWeekStatus.SettlementSubmitted)
        {
            Error = "El ajuste solo aplica en comprobación enviada de viático anticipado.";
            return RedirectToPage(new { id });
        }

        var difference = week.TotalAmount - week.ApprovedAdvanceAmount;
        if (difference == 0m)
        {
            Error = "No hay diferencia para generar ajuste.";
            return RedirectToPage(new { id });
        }

        var isBonus = string.Equals(mode, "bonus", StringComparison.OrdinalIgnoreCase);
        var isDeduct = string.Equals(mode, "deduct", StringComparison.OrdinalIgnoreCase);

        if (!isBonus && !isDeduct)
        {
            Error = "Modo de ajuste inválido.";
            return RedirectToPage(new { id });
        }

        if (difference > 0m && !isBonus)
        {
            Error = "Esta diferencia es positiva. Debes usar Abonar.";
            return RedirectToPage(new { id });
        }

        if (difference < 0m && !isDeduct)
        {
            Error = "Esta diferencia es negativa. Debes usar Descontar.";
            return RedirectToPage(new { id });
        }

        var marker = $"[VIATICO:{week.Id}]";
        var exists = await _db.EmployeeDeductions.AnyAsync(x => x.UserId == week.UserId && x.Concept.Contains(marker));
        if (exists)
        {
            Info = "Ya existe un ajuste de nómina para esta comprobación.";
            return RedirectToPage(new { id });
        }

        var localToday = DateTime.Now.Date;
        var periodStart = localToday.Day <= 15
            ? new DateTime(localToday.Year, localToday.Month, 1)
            : new DateTime(localToday.Year, localToday.Month, 16);
        var periodEnd = localToday.Day <= 15
            ? new DateTime(localToday.Year, localToday.Month, 15)
            : new DateTime(localToday.Year, localToday.Month, DateTime.DaysInMonth(localToday.Year, localToday.Month));

        var amount = Math.Abs(Math.Round(difference, 2));
        var conceptAction = difference > 0m ? "Abono" : "Descuento";

        var deduction = new EmployeeDeduction
        {
            UserId = week.UserId,
            Concept = $"Ajuste viático anticipado {week.WeekStartDate:yyyy-MM-dd} {marker} ({conceptAction})",
            Type = EmployeeDeductionType.Otro,
            Direction = difference > 0m ? EmployeeDeductionDirection.Bonus : EmployeeDeductionDirection.Deduct,
            Mode = EmployeeDeductionMode.FixedAmount,
            Frequency = EmployeeDeductionFrequency.Biweekly,
            Amount = amount,
            Rate = 0m,
            StartDate = periodStart,
            EndDate = periodEnd,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.EmployeeDeductions.Add(deduction);
        await _db.SaveChangesAsync();

        Info = $"Ajuste creado correctamente: {(difference > 0m ? "abono" : "descuento")} {amount:C}.";
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
