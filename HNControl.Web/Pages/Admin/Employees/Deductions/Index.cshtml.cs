using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
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
    public decimal DeductTotal { get; private set; }
    public decimal BonusTotal { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return RedirectToPage("/Admin/Employees/Index");

        Employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
        if (Employee == null) return NotFound();

        // Cierre automático: vencidos o préstamos con saldo <= 0
        await FinalizeExpiredAsync(UserId);

        var deds = await _db.EmployeeDeductions
            .AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.StartDate)
            .ToListAsync();

        var latestReview = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == UserId)
            .OrderByDescending(r => r.PeriodStart)
            .ThenByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        var variablePct = latestReview?.VariablePercent ?? 0m;
        if (variablePct < 0m) variablePct = 0m;
        if (variablePct > 1m) variablePct = 1m;
        var baseQ = Employee.SalaryBase / 2m;
        var estimatedQ = Math.Round((baseQ * 0.80m) + (baseQ * 0.20m * variablePct), 2);
        var today = AppTime.Today;

        // Ledger real (fuente de verdad del avance/saldo). Si un ajuste tiene aplicaciones
        // registradas, mandan sobre la inferencia por fechas.
        var ledger = (await _db.EmployeeDeductionApplications
            .AsNoTracking()
            .Where(a => a.UserId == UserId)
            .GroupBy(a => a.DeductionId)
            .Select(g => new { DeductionId = g.Key, Paid = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.DeductionId, x => (x.Paid, x.Count));

        Items = deds.Select(d =>
        {
            var eval = PayrollDeductionMath.EvaluateForDate(d, baseQ, estimatedQ, today);

            decimal? perPeriod = eval.AmountForPeriod;
            decimal? remaining = eval.RemainingToDate;
            int? paidPeriods = eval.PaidPeriods;
            int? totalPeriods = eval.TotalPeriods;

            if (ledger.TryGetValue(d.Id, out var led))
            {
                var scheduled = PayrollDeductionMath.ResolveAmount(d, baseQ, estimatedQ);
                var effectiveTotal = PayrollDeductionMath.EffectiveTotalAmount(d, scheduled);
                paidPeriods = led.Count;
                totalPeriods = PayrollDeductionMath.ResolveTotalPeriods(d, scheduled);
                remaining = effectiveTotal.HasValue ? Math.Max(0m, effectiveTotal.Value - led.Paid) : (decimal?)null;
                // Importe del próximo periodo, respetando lo ya pagado.
                perPeriod = PayrollDeductionMath.ComputeLedgerPeriodAmount(d, baseQ, estimatedQ, led.Paid, led.Count);
            }

            return new Row
            {
                Id = d.Id,
                Concept = d.Concept,
                Type = d.Type,
                Direction = d.Direction,
                Mode = d.Mode,
                Frequency = d.Frequency,
                ApplyOnHalf = d.ApplyOnHalf,
                Amount = d.Amount,
                Rate = d.Rate,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                IsActive = d.IsActive,
                RemainingAmount = d.RemainingAmount,
                TotalAmount = d.TotalAmount,
                TermCount = d.TermCount,
                DisplayPerPeriodAmount = perPeriod,
                DisplayRemainingAmount = remaining,
                DisplayPaidPeriods = paidPeriods,
                DisplayTotalPeriods = totalPeriods
            };
        }).ToList();

        DeductTotal = Items.Where(x => x.IsActive && x.Direction == EmployeeDeductionDirection.Deduct).Sum(x => x.PerPeriodAmount);
        BonusTotal = Items.Where(x => x.IsActive && x.Direction == EmployeeDeductionDirection.Bonus).Sum(x => x.PerPeriodAmount);

        return Page();
    }

    public async Task<IActionResult> OnPostStopAsync(Guid id)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return RedirectToPage("/Admin/Employees/Index");

        var d = await _db.EmployeeDeductions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (d == null) return NotFound();

        d.IsActive = false;
        d.EndDate ??= AppTime.Today;
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
        public EmployeeDeductionFrequency Frequency { get; set; } = EmployeeDeductionFrequency.Biweekly;
        public EmployeeDeductionApplyOnHalf? ApplyOnHalf { get; set; }
        public decimal Amount { get; set; }
        public decimal Rate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public decimal? RemainingAmount { get; set; }
        public decimal? TotalAmount { get; set; }
        public int? TermCount { get; set; }
        public decimal? DisplayPerPeriodAmount { get; set; }
        public decimal? DisplayRemainingAmount { get; set; }
        public int? DisplayPaidPeriods { get; set; }
        public int? DisplayTotalPeriods { get; set; }

        public decimal PerPeriodAmount
        {
            get
            {
                if (DisplayPerPeriodAmount.HasValue && DisplayPerPeriodAmount.Value > 0m)
                    return Math.Round(DisplayPerPeriodAmount.Value, 2);

                var amount = Mode switch
                {
                    EmployeeDeductionMode.FixedAmount => Amount,
                    _ => Amount
                };
                return Math.Round(Math.Max(0m, amount), 2);
            }
        }

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

        public int? TotalPeriods
        {
            get
            {
                if (TermCount.HasValue && TermCount.Value > 0) return TermCount.Value;
                if (TotalAmount.HasValue && TotalAmount.Value > 0m && PerPeriodAmount > 0m)
                    return (int)Math.Ceiling(TotalAmount.Value / PerPeriodAmount);
                return null;
            }
        }

        public int? PaidPeriods
        {
            get
            {
                var total = TotalPeriods;
                if (!total.HasValue) return null;

                if (RemainingAmount.HasValue && TotalAmount.HasValue && TotalAmount.Value > 0m && PerPeriodAmount > 0m)
                {
                    var remainingPeriods = (int)Math.Ceiling(Math.Max(0m, (DisplayRemainingAmount ?? RemainingAmount).Value) / PerPeriodAmount);
                    var paid = total.Value - remainingPeriods;
                    if (paid < 0) paid = 0;
                    if (paid > total.Value) paid = total.Value;
                    return paid;
                }

                return null;
            }
        }

        public decimal? RemainingAmountView => DisplayRemainingAmount ?? RemainingAmount;
        public int? PaidPeriodsView => DisplayPaidPeriods ?? PaidPeriods;
        public int? TotalPeriodsView => DisplayTotalPeriods ?? TotalPeriods;
    }

    private async Task FinalizeExpiredAsync(string userId)
    {
        // El cierre por plazo/saldo lo gobierna el ledger al confirmar cada pago.
        // Aquí solo cerramos por vencimiento (EndDate) o saldo manual agotado.
        var today = AppTime.Today;
        var now = DateTime.UtcNow;

        var toClose = await _db.EmployeeDeductions
            .Where(d => d.UserId == userId && d.IsActive)
            .Where(d => (d.EndDate != null && d.EndDate.Value < today)
                        || (d.RemainingAmount != null && d.RemainingAmount.Value <= 0m))
            .ToListAsync();

        if (!toClose.Any()) return;

        foreach (var d in toClose)
        {
            d.IsActive = false;
            d.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
    }
}
