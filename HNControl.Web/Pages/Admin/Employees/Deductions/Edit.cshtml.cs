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

        d.Concept = Input.Concept.Trim();
        d.Direction = Input.Direction;
        d.Type = Input.Type;
        d.Mode = Input.Mode;
        var now = DateTime.UtcNow;

        var start = Input.StartDate ?? now.Date;
        var freq = Input.Frequency;
        var applyHalf = freq == EmployeeDeductionFrequency.Monthly
            ? (Input.ApplyOnHalf ?? EmployeeDeductionApplyOnHalf.First)
            : (EmployeeDeductionApplyOnHalf?)null;

        // Bono: aplicacion unica en quincena actual.
        if (Input.Direction == EmployeeDeductionDirection.Bonus)
        {
            var (bonusStart, bonusEnd) = ResolveCurrentPeriod(DateTime.Now.Date);
            start = bonusStart;
            Input.StartDate = bonusStart;
            Input.EndDate = bonusEnd;
            Input.Type = EmployeeDeductionType.Otro;
            Input.Mode = EmployeeDeductionMode.FixedAmount;
            freq = EmployeeDeductionFrequency.Biweekly;
            applyHalf = null;
            Input.TermCount = null;
        }

        var isLoan = Input.Type == EmployeeDeductionType.Prestamo;
        var termCount = isLoan ? Input.TermCount : null;

        DateTime? end = null;
        if (termCount.HasValue && termCount.Value > 0)
            end = CalcEndDate(start, freq, applyHalf, termCount.Value);
        else
            end = Input.EndDate;

        var rate = 0m;
        if (Input.Mode is EmployeeDeductionMode.PercentOfBase or EmployeeDeductionMode.PercentOfEstimatedPay)
        {
            rate = Math.Round(Input.RatePercent / 100m, 5);
            if (rate < 0m) rate = 0m;
            if (rate > 1m) rate = 1m;
        }

        // Limpiar valores que no aplican
        var amount = Input.Mode == EmployeeDeductionMode.FixedAmount ? Input.Amount : 0m;
        if (Input.Mode == EmployeeDeductionMode.FixedAmount) rate = 0m;

        decimal? totalAmount = isLoan ? Input.TotalAmount : null;
        decimal? remainingAmount = isLoan ? Input.RemainingAmount : null;

        if (isLoan && totalAmount.HasValue && !remainingAmount.HasValue)
            remainingAmount = totalAmount;

        d.Frequency = freq;
        d.ApplyOnHalf = applyHalf;
        d.TermCount = termCount;
        d.Amount = amount;
        d.Rate = rate;
        d.StartDate = start;
        d.EndDate = end;
        d.TotalAmount = totalAmount;
        d.RemainingAmount = remainingAmount;
        d.IsActive = Input.IsActive;
        d.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return RedirectToPage("Index", new { UserId });
    }

    public class InputModel
    {
        [Required, MaxLength(200)]
        public string Concept { get; set; } = "";

        public EmployeeDeductionDirection Direction { get; set; } = EmployeeDeductionDirection.Deduct;

        public EmployeeDeductionType Type { get; set; } = EmployeeDeductionType.Otro;
        public EmployeeDeductionMode Mode { get; set; } = EmployeeDeductionMode.FixedAmount;

        public EmployeeDeductionFrequency Frequency { get; set; } = EmployeeDeductionFrequency.Biweekly;
        public EmployeeDeductionApplyOnHalf? ApplyOnHalf { get; set; } = null;

        [Range(1, 1200)]
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

    private static DateTime CalcEndDate(DateTime start, EmployeeDeductionFrequency freq, EmployeeDeductionApplyOnHalf? applyHalf, int termCount)
    {
        if (termCount <= 0) return start;

        if (freq == EmployeeDeductionFrequency.Monthly)
        {
            var half = applyHalf ?? EmployeeDeductionApplyOnHalf.First;
            var y = start.Year;
            var m = start.Month;
            DateTime end = start;

            for (var i = 1; i <= termCount; i++)
            {
                end = half == EmployeeDeductionApplyOnHalf.First
                    ? new DateTime(y, m, 15)
                    : new DateTime(y, m, DateTime.DaysInMonth(y, m));

                if (i == termCount) break;

                m++;
                if (m == 13) { m = 1; y++; }
            }

            return end;
        }
        else
        {
            var half = start.Day <= 15 ? 1 : 2;
            var y = start.Year;
            var m = start.Month;
            DateTime end = start;

            for (var i = 1; i <= termCount; i++)
            {
                end = half == 1
                    ? new DateTime(y, m, 15)
                    : new DateTime(y, m, DateTime.DaysInMonth(y, m));

                if (i == termCount) break;

                if (half == 1)
                {
                    half = 2;
                }
                else
                {
                    half = 1;
                    m++;
                    if (m == 13) { m = 1; y++; }
                }
            }

            return end;
        }
    }

    private static (DateTime Start, DateTime End) ResolveCurrentPeriod(DateTime date)
    {
        if (date.Day <= 15)
            return (new DateTime(date.Year, date.Month, 1), new DateTime(date.Year, date.Month, 15));

        return (new DateTime(date.Year, date.Month, 16),
            new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));
    }
}
