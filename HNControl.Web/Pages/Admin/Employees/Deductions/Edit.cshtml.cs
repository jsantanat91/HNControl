using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
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
            ApplyOnHalf = d.ApplyOnHalf ?? 2,
            AutoLoan = d.Type == EmployeeDeductionType.Prestamo && d.TermCount.HasValue,
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
        d.Frequency = Input.Frequency;
        d.ApplyOnHalf = Input.Frequency == EmployeeDeductionFrequency.Mensual ? (Input.ApplyOnHalf ?? 2) : null;
        d.Amount = Input.Amount;
        var now = DateTime.UtcNow;

        var start = Input.StartDate ?? now.Date;
        DateTime? end = Input.EndDate;

        if (Input.Type == EmployeeDeductionType.PensionAlimenticia)
        {
            Input.Direction = EmployeeDeductionDirection.Deduct;
            Input.Frequency = EmployeeDeductionFrequency.Quincenal;
            end = null;
        }

        var isAutoLoan = Input.Type == EmployeeDeductionType.Prestamo && Input.AutoLoan;
        if (isAutoLoan)
        {
            if (Input.TotalAmount is null || Input.TotalAmount <= 0m)
                ModelState.AddModelError("Input.TotalAmount", "Para préstamo automático necesitas el Total del préstamo.");
            if (Input.TermCount is null || Input.TermCount <= 0)
                ModelState.AddModelError("Input.TermCount", "Para préstamo automático necesitas los Plazos (número de pagos). ");
            if (Input.Frequency == EmployeeDeductionFrequency.Mensual && (Input.ApplyOnHalf is null or < 1 or > 2))
                ModelState.AddModelError("Input.ApplyOnHalf", "Para mensual selecciona en qué quincena se cobra (1 o 2).");

            if (!ModelState.IsValid) return Page();

            Input.Direction = EmployeeDeductionDirection.Deduct;
            Input.Mode = EmployeeDeductionMode.FixedAmount;

            var per = Input.Amount;
            if (per <= 0m)
                per = Math.Round(Input.TotalAmount.Value / Input.TermCount.Value, 2);
            if (per < 0m) per = 0m;
            Input.Amount = per;
            Input.RemainingAmount = Input.TotalAmount;

            end = PayPeriodUtil.ComputeLoanEndDate(start, Input.Frequency, Input.ApplyOnHalf, Input.TermCount.Value);
        }

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
        d.TermCount = isAutoLoan ? Input.TermCount : null;
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

        public EmployeeDeductionFrequency Frequency { get; set; } = EmployeeDeductionFrequency.Quincenal;

        [Range(1, 2)]
        public int? ApplyOnHalf { get; set; } = 2;

        public bool AutoLoan { get; set; } = true;

        [Range(1, 120)]
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
