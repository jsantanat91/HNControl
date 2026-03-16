using HNControl.Web.Models;

namespace HNControl.Web.Services;

public sealed record PayrollDeductionEval(
    decimal AmountForPeriod,
    decimal? RemainingToDate,
    int? PaidPeriods,
    int? TotalPeriods);

public static class PayrollDeductionMath
{
    public static (decimal deductions, decimal bonuses, List<PayrollAdjustmentLine> lines) CalculateTotals(
        IEnumerable<EmployeeDeduction> deductions,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var start = periodStart.Date;
        var end = periodEnd.Date;
        if (end < start)
            (start, end) = (end, start);

        decimal totalDeductions = 0m;
        decimal totalBonuses = 0m;
        var lines = new List<PayrollAdjustmentLine>();

        foreach (var d in deductions)
        {
            var eval = EvaluateForPeriod(d, baseQuincenal, estimatedQuincenal, start, end);
            if (eval.AmountForPeriod <= 0m) continue;

            if (d.Direction == EmployeeDeductionDirection.Bonus)
            {
                totalBonuses += eval.AmountForPeriod;
                lines.Add(new PayrollAdjustmentLine(d.Concept, "Bono", eval.AmountForPeriod));
            }
            else
            {
                totalDeductions += eval.AmountForPeriod;
                lines.Add(new PayrollAdjustmentLine(d.Concept, "Deduccion", eval.AmountForPeriod));
            }
        }

        return (
            Math.Round(totalDeductions, 2),
            Math.Round(totalBonuses, 2),
            lines.OrderBy(x => x.Kind).ThenBy(x => x.Concept).ToList());
    }

    public static (decimal deductions, decimal bonuses, List<PayrollAdjustmentLine> lines) CalculateTotals(
        IEnumerable<EmployeeDeduction> deductions,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodDate)
    {
        decimal totalDeductions = 0m;
        decimal totalBonuses = 0m;
        var lines = new List<PayrollAdjustmentLine>();

        foreach (var d in deductions)
        {
            var eval = EvaluateForDate(d, baseQuincenal, estimatedQuincenal, periodDate);
            if (eval.AmountForPeriod <= 0m) continue;

            if (d.Direction == EmployeeDeductionDirection.Bonus)
            {
                totalBonuses += eval.AmountForPeriod;
                lines.Add(new PayrollAdjustmentLine(d.Concept, "Bono", eval.AmountForPeriod));
            }
            else
            {
                totalDeductions += eval.AmountForPeriod;
                lines.Add(new PayrollAdjustmentLine(d.Concept, "Deduccion", eval.AmountForPeriod));
            }
        }

        return (
            Math.Round(totalDeductions, 2),
            Math.Round(totalBonuses, 2),
            lines.OrderBy(x => x.Kind).ThenBy(x => x.Concept).ToList());
    }

    public static PayrollDeductionEval EvaluateForPeriod(
        EmployeeDeduction d,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var start = periodStart.Date;
        var end = periodEnd.Date;
        if (end < start)
            (start, end) = (end, start);

        if (!d.IsActive || d.StartDate.Date > end || (d.EndDate.HasValue && d.EndDate.Value.Date < start))
            return new PayrollDeductionEval(0m, ResolveRemaining(d, 0m, end), ResolvePaidPeriods(d, 0m, end), ResolveTotalPeriods(d, 0m));

        if (!OccursInPeriod(d, start, end))
            return new PayrollDeductionEval(0m, ResolveRemaining(d, 0m, end), ResolvePaidPeriods(d, 0m, end), ResolveTotalPeriods(d, 0m));

        return EvaluateForDate(d, baseQuincenal, estimatedQuincenal, end);
    }

    public static PayrollDeductionEval EvaluateForDate(
        EmployeeDeduction d,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodDate)
    {
        var localDate = periodDate.Date;

        if (!d.IsActive || d.StartDate.Date > localDate || (d.EndDate.HasValue && d.EndDate.Value.Date < localDate))
            return new PayrollDeductionEval(0m, ResolveRemaining(d, 0m, localDate), ResolvePaidPeriods(d, 0m, localDate), ResolveTotalPeriods(d, 0m));

        if (!AppliesOnDate(d, localDate))
            return new PayrollDeductionEval(0m, ResolveRemaining(d, 0m, localDate), ResolvePaidPeriods(d, 0m, localDate), ResolveTotalPeriods(d, 0m));

        var amount = ResolveAmount(d, baseQuincenal, estimatedQuincenal);
        if (amount <= 0m)
            return new PayrollDeductionEval(0m, ResolveRemaining(d, amount, localDate), ResolvePaidPeriods(d, amount, localDate), ResolveTotalPeriods(d, amount));

        var totalPeriods = ResolveTotalPeriods(d, amount);
        var paidPeriods = ResolvePaidPeriods(d, amount, localDate);
        var remainingToDate = ResolveRemaining(d, amount, localDate);

        if (d.Type == EmployeeDeductionType.Prestamo)
        {
            if (totalPeriods.HasValue && totalPeriods.Value > 0)
            {
                var occurrenceIndex = CountPeriodsOccurred(d.StartDate.Date, localDate, d.Frequency, d.ApplyOnHalf);
                if (occurrenceIndex > totalPeriods.Value)
                    amount = 0m;
            }

            if (remainingToDate.HasValue && remainingToDate.Value <= 0m)
                amount = 0m;

            if (d.TotalAmount.HasValue && d.TotalAmount.Value > 0m)
            {
                var occurrenceIndex = Math.Max(1, CountPeriodsOccurred(d.StartDate.Date, localDate, d.Frequency, d.ApplyOnHalf));
                var periodsBeforeCurrent = Math.Max(0, occurrenceIndex - 1);
                if (totalPeriods.HasValue)
                    periodsBeforeCurrent = Math.Min(periodsBeforeCurrent, totalPeriods.Value);

                var remainingBeforeCurrent = Math.Max(0m, d.TotalAmount.Value - (periodsBeforeCurrent * amount));
                amount = Math.Min(amount, remainingBeforeCurrent);
            }
        }
        else if (d.RemainingAmount.HasValue)
        {
            if (d.RemainingAmount.Value <= 0m) amount = 0m;
            else amount = Math.Min(amount, d.RemainingAmount.Value);
        }

        amount = Math.Round(Math.Max(0m, amount), 2);
        return new PayrollDeductionEval(amount, remainingToDate, paidPeriods, totalPeriods);
    }

    public static decimal ResolveAmount(EmployeeDeduction d, decimal baseQuincenal, decimal estimatedQuincenal)
    {
        var amount = d.Mode switch
        {
            EmployeeDeductionMode.FixedAmount => d.Amount,
            EmployeeDeductionMode.PercentOfBase => baseQuincenal * d.Rate,
            EmployeeDeductionMode.PercentOfEstimatedPay => estimatedQuincenal * d.Rate,
            _ => d.Amount
        };

        return Math.Round(Math.Max(0m, amount), 2);
    }

    public static bool AppliesOnDate(EmployeeDeduction d, DateTime date)
    {
        if (d.Frequency == EmployeeDeductionFrequency.Biweekly)
            return true;

        var currentHalf = date.Day <= 15
            ? EmployeeDeductionApplyOnHalf.First
            : EmployeeDeductionApplyOnHalf.Second;

        return d.ApplyOnHalf == null || d.ApplyOnHalf == currentHalf;
    }

    public static bool OccursInPeriod(EmployeeDeduction d, DateTime periodStart, DateTime periodEnd)
    {
        var start = periodStart.Date;
        var end = periodEnd.Date;
        if (end < start)
            (start, end) = (end, start);

        var firstApplicable = d.StartDate.Date;
        var effectiveStart = firstApplicable > start ? firstApplicable : start;
        if (effectiveStart > end)
            return false;

        var before = CountPeriodsOccurred(d.StartDate.Date, effectiveStart.AddDays(-1), d.Frequency, d.ApplyOnHalf);
        var inEnd = CountPeriodsOccurred(d.StartDate.Date, end, d.Frequency, d.ApplyOnHalf);
        return inEnd - before > 0;
    }

    public static int? ResolveTotalPeriods(EmployeeDeduction d, decimal periodAmount)
    {
        if (d.TermCount.HasValue && d.TermCount.Value > 0)
            return d.TermCount.Value;

        if (d.TotalAmount.HasValue && d.TotalAmount.Value > 0m && periodAmount > 0m)
            return (int)Math.Ceiling(d.TotalAmount.Value / periodAmount);

        return null;
    }

    public static int? ResolvePaidPeriods(EmployeeDeduction d, decimal periodAmount, DateTime asOfDate)
    {
        var total = ResolveTotalPeriods(d, periodAmount);
        if (!total.HasValue || total.Value <= 0) return null;

        var occurred = CountPeriodsOccurred(d.StartDate.Date, asOfDate.Date, d.Frequency, d.ApplyOnHalf);
        if (occurred < 0) occurred = 0;
        if (occurred > total.Value) occurred = total.Value;
        return occurred;
    }

    public static decimal? ResolveRemaining(EmployeeDeduction d, decimal periodAmount, DateTime asOfDate)
    {
        if (d.Type == EmployeeDeductionType.Prestamo && d.TotalAmount.HasValue && d.TotalAmount.Value > 0m && periodAmount > 0m)
        {
            var totalPeriods = ResolveTotalPeriods(d, periodAmount) ?? int.MaxValue;
            var occurred = CountPeriodsOccurred(d.StartDate.Date, asOfDate.Date, d.Frequency, d.ApplyOnHalf);
            var paid = Math.Min(totalPeriods, Math.Max(0, occurred));
            var bySchedule = Math.Max(0m, d.TotalAmount.Value - (paid * periodAmount));

            if (d.RemainingAmount.HasValue)
                return Math.Min(bySchedule, Math.Max(0m, d.RemainingAmount.Value));

            return bySchedule;
        }

        return d.RemainingAmount;
    }

    public static int CountPeriodsOccurred(
        DateTime start,
        DateTime end,
        EmployeeDeductionFrequency frequency,
        EmployeeDeductionApplyOnHalf? applyOnHalf)
    {
        if (end < start) return 0;

        if (frequency == EmployeeDeductionFrequency.Biweekly)
        {
            var count = 0;
            var cursor = start.Date;
            while (cursor <= end.Date)
            {
                count++;
                cursor = cursor.Day <= 15
                    ? new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month))
                    : new DateTime(cursor.Year, cursor.Month, 15).AddMonths(1);
            }
            return count;
        }

        var half = applyOnHalf ?? EmployeeDeductionApplyOnHalf.First;
        var monthCursor = new DateTime(start.Year, start.Month, 1);
        var countMonthly = 0;

        while (monthCursor <= end.Date)
        {
            var day = half == EmployeeDeductionApplyOnHalf.First
                ? 15
                : DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
            var hit = new DateTime(monthCursor.Year, monthCursor.Month, day);
            if (hit >= start.Date && hit <= end.Date) countMonthly++;
            monthCursor = monthCursor.AddMonths(1);
        }

        return countMonthly;
    }
}
