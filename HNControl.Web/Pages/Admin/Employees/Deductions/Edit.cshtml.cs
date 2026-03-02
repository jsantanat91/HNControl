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
        d.Amount = Input.Amount;
        var now = DateTime.UtcNow;

        var start = Input.StartDate ?? now.Date;
        DateTime? end = Input.EndDate;

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

    public class InputModel
    {
        [Required, MaxLength(200)]
        public string Concept { get; set; } = "";

        public EmployeeDeductionDirection Direction { get; set; } = EmployeeDeductionDirection.Deduct;

        public EmployeeDeductionType Type { get; set; } = EmployeeDeductionType.Otro;
        public EmployeeDeductionMode Mode { get; set; } = EmployeeDeductionMode.FixedAmount;

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
