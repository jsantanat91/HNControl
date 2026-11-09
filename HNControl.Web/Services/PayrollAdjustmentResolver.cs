using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
namespace HNControl.Web.Services;

public static class PayrollAdjustmentResolver
{
    public static async Task<(decimal deductions, decimal bonuses, List<PayrollAdjustmentLine> lines)> CalculateAsync(
        ApplicationDbContext db, string userId, decimal baseQuincenal, decimal estimatedQuincenal, DateTime periodStart, DateTime periodEnd)
    {
        var applied = await db.EmployeeDeductionApplications.AsNoTracking()
            .Where(x => x.UserId == userId && x.PeriodStart == periodStart.Date && x.PeriodEnd == periodEnd.Date).ToListAsync();
        var appliedIds = applied.Select(x => x.DeductionId).ToList();
        var active = await db.EmployeeDeductions
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive && !appliedIds.Contains(d.Id))
            .Where(d => d.StartDate <= periodEnd && (d.EndDate == null || d.EndDate >= periodStart))
            .ToListAsync();

        var (deductions, bonuses, lines) = PayrollDeductionMath.CalculateTotals(active, baseQuincenal, estimatedQuincenal, periodStart, periodEnd);
        foreach (var row in applied)
        {
            if (row.Direction == EmployeeDeductionDirection.Bonus) bonuses += row.Amount; else deductions += row.Amount;
            lines.Add(new PayrollAdjustmentLine(row.Concept, row.Direction == EmployeeDeductionDirection.Bonus ? "Bono" : "Deduccion", row.Amount));
        }
        return (Math.Round(deductions, 2), Math.Round(bonuses, 2), lines);
    }
}
