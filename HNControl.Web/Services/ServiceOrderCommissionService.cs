using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public static class ServiceOrderCommissionService
{
    public static (DateTime start, DateTime end) Period(DateTime utcNow)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        var start = new DateTime(local.Year, local.Month, local.Day <= 15 ? 1 : 16);
        var end = local.Day <= 15 ? new DateTime(local.Year, local.Month, 15) : new DateTime(local.Year, local.Month, DateTime.DaysInMonth(local.Year, local.Month));
        return (TimeUtil.UtcDate(start), TimeUtil.UtcDate(end));
    }

    // Saving the final status and its commission is one transaction. Row lock serializes retries.
    public static async Task FinalizeAsync(ApplicationDbContext db, ServiceOrder order, DateTime utcNow)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var source = db.Database.IsNpgsql()
            ? db.ServiceOrders.FromSqlInterpolated($"SELECT * FROM \"ServiceOrders\" WHERE \"Id\" = {order.Id} FOR UPDATE")
            : db.ServiceOrders.Where(x => x.Id == order.Id);
        var previous = await source.AsNoTracking().SingleAsync();
        order.Status = ServiceOrderStatus.Finalized;
        order.FinalizedAt = previous.Status == ServiceOrderStatus.Finalized ? previous.FinalizedAt ?? utcNow : utcNow;
        if (previous.Status != ServiceOrderStatus.Finalized && !await db.EmployeeDeductions.AnyAsync(x => x.SourceServiceOrderId == order.Id))
        {
            var userId = order.ClaimedByUserId ?? order.AssignedUserId;
            var employee = await db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive);
            if (employee is { ServiceOrderCommissionEnabled: true, ServiceOrderCommissionAmount: > 0 })
            {
                var (start, end) = Period(utcNow);
                // A paid period is immutable; accrue into the next unpaid fortnight.
                while (await db.PayrollReceiptDispatches.AnyAsync(x => x.UserId == employee.UserId && x.PeriodStart == start && x.PeriodEnd == end && x.IsSent))
                    (start, end) = Period(end.AddDays(1).AddHours(12));
                db.EmployeeDeductions.Add(new EmployeeDeduction
                {
                    UserId = employee.UserId, SourceServiceOrderId = order.Id,
                    Type = EmployeeDeductionType.ComisionOrdenServicio, Direction = EmployeeDeductionDirection.Bonus,
                    Concept = $"Comisión OS-{order.Id.ToString("N")[..8].ToUpperInvariant()}",
                    Mode = EmployeeDeductionMode.FixedAmount, Frequency = EmployeeDeductionFrequency.Biweekly,
                    Amount = Math.Round(employee.ServiceOrderCommissionAmount, 2), TermCount = 1,
                    StartDate = start, EndDate = end, IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow
                });
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
