using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees.Deductions;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public EditModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public EmployeeProfile? Employee { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || Id == Guid.Empty)
            return RedirectToPage("Index");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
        if (Employee == null) return NotFound();

        var d = await _db.EmployeeDeductions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id && x.UserId == UserId);
        if (d == null) return NotFound();

        Input = new InputModel
        {
            Concept = d.Concept,
            Direction = d.Direction,
            Type = d.Type,
            Mode = d.Mode,
            Frequency = d.Frequency,
            ApplyOnHalf = d.ApplyOnHalf,
            TermCount = d.TermCount,
            Amount = d.Amount,
            RatePercent = Math.Round(d.Rate * 100m, 2),
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            TotalAmount = d.TotalAmount,
            RemainingAmount = d.RemainingAmount,
            IsActive = d.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || Id == Guid.Empty)
            return RedirectToPage("Index");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
        if (Employee == null) return NotFound();

        if (!ModelState.IsValid) return Page();

        var d = await _db.EmployeeDeductions.FirstOrDefaultAsync(x => x.Id == Id && x.UserId == UserId);
        if (d == null) return NotFound();

        var now = DateTime.UtcNow;

        var start = Input.StartDate ?? now.Date;
        DateTime? end = Input.EndDate;

        // Sanitizar frecuencia / plazo
        var freq = Input.Frequency;
        var applyHalf = (freq == EmployeeDeductionFrequency.Monthly)
            ? ((Input.ApplyOnHalf is 1 or 2) ? Input.ApplyOnHalf : 2)
            : null;
        var termCount = (Input.TermCount.HasValue && Input.TermCount.Value > 0) ? Input.TermCount : null;

        // Para préstamos: si capturas Total pero no Saldo, asumimos saldo inicial = total
        if (Input.Type == EmployeeDeductionType.Prestamo && Input.TotalAmount.HasValue && !Input.RemainingAmount.HasValue)
            Input.RemainingAmount = Input.TotalAmount;

        // Si viene un plazo y no capturaste fin, lo calculamos automáticamente
        if (end == null && termCount.HasValue)
            end = CalcAutoEndDate(start, freq, applyHalf, termCount.Value);

        d.Concept = Input.Concept.Trim();
        d.Direction = Input.Direction;
        d.Type = Input.Type;
        d.Mode = Input.Mode;
        d.Frequency = freq;
        d.ApplyOnHalf = applyHalf;
        d.TermCount = termCount;
        d.Amount = Input.Amount;
        var rate = 0m;
        if (Input.Mode is EmployeeDeductionMode.PercentOfBase or EmployeeDeductionMode.PercentOfEstimatedPay)
        {
            rate = Math.Round(Input.RatePercent / 100m, 5);
            if (rate < 0m) rate = 0m;
            if (rate > 1m) rate = 1m;
        }

        d.Rate = rate;
        d.StartDate = start;
        d.EndDate = end;
        d.TotalAmount = Input.TotalAmount;
        d.RemainingAmount = Input.RemainingAmount;
        d.IsActive = Input.IsActive;
        d.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return RedirectToPage("Index", new { UserId });
    }

    private static DateTime CalcAutoEndDate(DateTime start, EmployeeDeductionFrequency freq, int? applyHalf, int termCount)
    {
        if (termCount <= 0) return start;

        var firstStart = freq switch
        {
            EmployeeDeductionFrequency.Biweekly => GetBiweeklyPeriodStart(start),
            EmployeeDeductionFrequency.Monthly => GetMonthlyPeriodStart(start, (applyHalf is 1 or 2) ? applyHalf.Value : 2),
            _ => GetBiweeklyPeriodStart(start)
        };

        var lastStart = firstStart;
        for (var i = 1; i < termCount; i++)
        {
            lastStart = freq switch
            {
                EmployeeDeductionFrequency.Biweekly => NextBiweeklyStart(lastStart),
                EmployeeDeductionFrequency.Monthly => lastStart.AddMonths(1),
                _ => NextBiweeklyStart(lastStart)
            };
        }

        return GetPeriodEnd(lastStart);
    }

    private static DateTime GetBiweeklyPeriodStart(DateTime d)
        => d.Day <= 15 ? new DateTime(d.Year, d.Month, 1) : new DateTime(d.Year, d.Month, 16);

    private static DateTime GetMonthlyPeriodStart(DateTime d, int applyHalf)
    {
        if (applyHalf == 2) return new DateTime(d.Year, d.Month, 16);
        return d.Day <= 15
            ? new DateTime(d.Year, d.Month, 1)
            : new DateTime(d.AddMonths(1).Year, d.AddMonths(1).Month, 1);
    }

    private static DateTime NextBiweeklyStart(DateTime periodStart)
        => periodStart.Day == 1
            ? new DateTime(periodStart.Year, periodStart.Month, 16)
            : new DateTime(periodStart.AddMonths(1).Year, periodStart.AddMonths(1).Month, 1);

    private static DateTime GetPeriodEnd(DateTime periodStart)
    {
        if (periodStart.Day == 1) return new DateTime(periodStart.Year, periodStart.Month, 15);
        var last = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
        return new DateTime(periodStart.Year, periodStart.Month, last);
    }

    public class InputModel
    {
        [Required, MaxLength(200)]
        public string Concept { get; set; } = "";

        public EmployeeDeductionDirection Direction { get; set; } = EmployeeDeductionDirection.Deduct;

        public EmployeeDeductionType Type { get; set; } = EmployeeDeductionType.Otro;
        public EmployeeDeductionMode Mode { get; set; } = EmployeeDeductionMode.FixedAmount;

        public EmployeeDeductionFrequency Frequency { get; set; } = EmployeeDeductionFrequency.Biweekly;

        /// <summary>Solo para Frequency = Mensual: 1 = 1-15, 2 = 16-fin.</summary>
        public int? ApplyOnHalf { get; set; } = 2;

        /// <summary>
        /// Plazo en periodos:
        /// - Quincenal: quincenas
        /// - Mensual: meses
        /// </summary>
        [Range(1, 240)]
        public int? TermCount { get; set; } = null;

        [Range(0, 99999999)]
        public decimal Amount { get; set; } = 0m;

        [Range(0, 100)]
        public decimal RatePercent { get; set; } = 0m;

        public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
        public DateTime? EndDate { get; set; } = null;

        public decimal? TotalAmount { get; set; } = null;
        public decimal? RemainingAmount { get; set; } = null;

        public bool IsActive { get; set; } = true;
    }
}
