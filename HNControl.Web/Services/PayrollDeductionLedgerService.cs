using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public interface IPayrollDeductionLedger
{
    /// <summary>
    /// Asienta en el ledger las deducciones/bonos que aplican a un periodo confirmado.
    /// Idempotente por (deduccion, periodo). Cierra los ajustes que ya se completaron.
    /// </summary>
    Task RecordApplicationsAsync(string userId, DateTime periodStart, DateTime periodEnd, DateTime payrollDate, CancellationToken ct = default);
}

public class PayrollDeductionLedgerService : IPayrollDeductionLedger
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PayrollDeductionLedgerService> _log;

    public PayrollDeductionLedgerService(ApplicationDbContext db, ILogger<PayrollDeductionLedgerService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task RecordApplicationsAsync(string userId, DateTime periodStart, DateTime periodEnd, DateTime payrollDate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            var pStart = periodStart.Date;
            var pEnd = periodEnd.Date;
            if (pEnd < pStart) (pStart, pEnd) = (pEnd, pStart);

            var employee = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (employee == null) return;

            var variablePct = await _db.PerformanceReviews.AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.PeriodStart).ThenByDescending(r => r.UpdatedAt)
                .Select(r => (decimal?)r.VariablePercent)
                .FirstOrDefaultAsync(ct) ?? 0m;
            variablePct = Math.Clamp(variablePct, 0m, 1m);

            var baseQ = employee.SalaryBase / 2m;
            var estimatedQ = Math.Round((baseQ * 0.80m) + (baseQ * 0.20m * variablePct), 2);

            var deductions = await _db.EmployeeDeductions
                .Where(d => d.UserId == userId && d.IsActive
                    && d.StartDate <= pEnd && (d.EndDate == null || d.EndDate >= pStart))
                .ToListAsync(ct);

            foreach (var d in deductions)
            {
                // Idempotencia: no duplicar en el mismo periodo.
                var already = await _db.EmployeeDeductionApplications
                    .AnyAsync(a => a.DeductionId == d.Id && a.PeriodStart == pStart && a.PeriodEnd == pEnd, ct);
                if (already) continue;

                // ¿Aplica en este periodo? (ocurrencia + quincena para mensuales)
                if (!PayrollDeductionMath.OccursInPeriod(d, pStart, pEnd)) continue;
                if (!PayrollDeductionMath.AppliesOnDate(d, pEnd)) continue;

                var paidAmount = await _db.EmployeeDeductionApplications
                    .Where(a => a.DeductionId == d.Id).SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
                var paidCount = await _db.EmployeeDeductionApplications
                    .CountAsync(a => a.DeductionId == d.Id, ct);

                var amount = PayrollDeductionMath.ComputeLedgerPeriodAmount(d, baseQ, estimatedQ, paidAmount, paidCount);
                if (amount <= 0m)
                {
                    // Sin importe: si ya se completó, cerramos.
                    if (PayrollDeductionMath.IsLedgerCompleted(d, baseQ, estimatedQ, paidAmount, paidCount))
                        Close(d, pEnd);
                    continue;
                }

                _db.EmployeeDeductionApplications.Add(new EmployeeDeductionApplication
                {
                    DeductionId = d.Id,
                    UserId = userId,
                    PeriodStart = pStart,
                    PeriodEnd = pEnd,
                    PayrollDate = payrollDate.Date,
                    Amount = amount,
                    Direction = d.Direction,
                    Concept = d.Concept,
                    CreatedAt = DateTime.UtcNow
                });

                if (PayrollDeductionMath.IsLedgerCompleted(d, baseQ, estimatedQ, paidAmount + amount, paidCount + 1))
                    Close(d, pEnd);
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // No romper la confirmación de pago por un fallo del ledger.
            _log.LogWarning(ex, "No se pudo registrar ledger de deducciones de {User} {Start}-{End}", userId, periodStart, periodEnd);
        }
    }

    private void Close(EmployeeDeduction d, DateTime periodEnd)
    {
        var tracked = _db.EmployeeDeductions.Local.FirstOrDefault(x => x.Id == d.Id) ?? d;
        tracked.IsActive = false;
        tracked.EndDate ??= periodEnd;
        tracked.UpdatedAt = DateTime.UtcNow;
    }
}
