using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees.Deductions;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    public EmployeeProfile? Employee { get; private set; }
    public List<Row> Items { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return RedirectToPage("/Admin/Employees/Index");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
        if (Employee == null) return NotFound();

        var deds = await _db.EmployeeDeductions
            .AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.StartDate)
            .ToListAsync();

        Items = deds.Select(d => new Row
        {
            Id = d.Id,
            Concept = d.Concept,
            Type = d.Type,
            Direction = d.Direction,
            Mode = d.Mode,
            Amount = d.Amount,
            Rate = d.Rate,
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            IsActive = d.IsActive,
            RemainingAmount = d.RemainingAmount
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostStopAsync(Guid id)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return RedirectToPage("/Admin/Employees/Index");

        var d = await _db.EmployeeDeductions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (d == null) return NotFound();

        d.IsActive = false;
        d.EndDate ??= DateTime.UtcNow.Date;
        d.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToPage(new { UserId });
    }

    public class Row
    {
        public Guid Id { get; set; }
        public string Concept { get; set; } = "";
        public EmployeeDeductionType Type { get; set; }
        public EmployeeDeductionDirection Direction { get; set; } = EmployeeDeductionDirection.Deduct;
        public EmployeeDeductionMode Mode { get; set; }
        public decimal Amount { get; set; }
        public decimal Rate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public decimal? RemainingAmount { get; set; }

        public string AmountLabel
        {
            get
            {
                var baseLabel = Mode switch
                {
                    EmployeeDeductionMode.FixedAmount => Amount.ToString("N2"),
                    _ => (Rate * 100m).ToString("0.##") + "%"
                };

                return Direction == EmployeeDeductionDirection.Bonus ? "+" + baseLabel : baseLabel;
            }
        }
    }
}
