using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees.Deductions;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    public EmployeeProfile? Employee { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) return RedirectToPage("Index");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
        if (Employee == null) return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) return RedirectToPage("Index");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
        if (Employee == null) return NotFound();

        if (!ModelState.IsValid) return Page();

        var now = DateTime.UtcNow;

        var start = Input.StartDate ?? now.Date;
        var freq = Input.Frequency;
        var applyHalf = freq == EmployeeDeductionFrequency.Monthly
            ? (Input.ApplyOnHalf ?? EmployeeDeductionApplyOnHalf.First)
            : (EmployeeDeductionApplyOnHalf?)null;

        var isLoan = Input.Type == EmployeeDeductionType.Prestamo;
        var termCount = isLoan ? Input.TermCount : null;

        // Fin: si hay plazo, el fin lo calculamos (ignora EndDate manual)
        DateTime? end = null;
        if (termCount.HasValue && termCount.Value > 0)
            end = CalcEndDate(start, freq, applyHalf, termCount.Value);
        else
            end = Input.EndDate;

        // Guardamos porcentaje "humano" (15 = 15%) y lo convertimos a 0..1
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

        // Si es préstamo y capturó total pero no saldo: arrancamos saldo = total
        if (isLoan && totalAmount.HasValue && !remainingAmount.HasValue)
            remainingAmount = totalAmount;

        var d = new EmployeeDeduction
        {
            UserId = UserId,
            Concept = Input.Concept.Trim(),
            Type = Input.Type,
            Direction = Input.Direction,
            Mode = Input.Mode,
            Frequency = freq,
            ApplyOnHalf = applyHalf,
            TermCount = termCount,
            Amount = amount,
            Rate = rate,
            StartDate = start,
            EndDate = end,
            TotalAmount = totalAmount,
            RemainingAmount = remainingAmount,
            IsActive = Input.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.EmployeeDeductions.Add(d);
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
}
