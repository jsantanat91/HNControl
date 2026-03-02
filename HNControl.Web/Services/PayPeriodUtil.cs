using HNControl.Web.Models;

namespace HNControl.Web.Services;

/// <summary>
/// Utilidades de periodos de pago (quincena 1: 1-15, quincena 2: 16-fin de mes).
/// Se usa para:
/// - Deducciones mensuales (aplican solo en 1 quincena del mes)
/// - Préstamos automáticos (terminan por plazos)
/// </summary>
public static class PayPeriodUtil
{
    public readonly record struct PayPeriod(int Year, int Month, int Half);

    public static PayPeriod FromDate(DateTime d)
    {
        var half = d.Day <= 15 ? 1 : 2;
        return new PayPeriod(d.Year, d.Month, half);
    }

    public static DateTime PeriodStart(PayPeriod p)
        => p.Half == 1
            ? new DateTime(p.Year, p.Month, 1)
            : new DateTime(p.Year, p.Month, 16);

    public static DateTime PeriodEnd(PayPeriod p)
        => p.Half == 1
            ? new DateTime(p.Year, p.Month, 15)
            : new DateTime(p.Year, p.Month, DateTime.DaysInMonth(p.Year, p.Month));

    public static PayPeriod Next(PayPeriod p)
    {
        if (p.Half == 1) return new PayPeriod(p.Year, p.Month, 2);
        var dt = new DateTime(p.Year, p.Month, 1).AddMonths(1);
        return new PayPeriod(dt.Year, dt.Month, 1);
    }

    public static PayPeriod Add(PayPeriod p, int steps)
    {
        var cur = p;
        for (var i = 0; i < steps; i++) cur = Next(cur);
        return cur;
    }

    public static bool IsDueThisPeriod(EmployeeDeduction d, PayPeriod current)
    {
        // Por rango de fechas, el query ya lo filtra; aquí solo frecuencia.
        if (d.Frequency == EmployeeDeductionFrequency.Quincenal) return true;

        // Mensual: aplica solo en una quincena del mes.
        var half = d.ApplyOnHalf ?? 2; // default: 2da quincena
        return current.Half == half;
    }

    /// <summary>
    /// Calcula EndDate para préstamos automáticos (por plazos).
    /// Devuelve el fin de la quincena donde cae el último pago.
    /// </summary>
    public static DateTime ComputeLoanEndDate(DateTime startDate, EmployeeDeductionFrequency frequency, int? applyOnHalf, int termCount)
    {
        if (termCount <= 0) termCount = 1;

        if (frequency == EmployeeDeductionFrequency.Quincenal)
        {
            var first = FromDate(startDate);
            var lastPeriod = Add(first, termCount - 1);
            return PeriodEnd(lastPeriod);
        }

        // Mensual
        var half = applyOnHalf ?? 2;
        var monthAnchor = new DateTime(startDate.Year, startDate.Month, 1);
        var firstInMonth = new PayPeriod(monthAnchor.Year, monthAnchor.Month, half);
        var firstDueDate = PeriodEnd(firstInMonth);
        if (startDate.Date > firstDueDate.Date)
        {
            var nextMonth = monthAnchor.AddMonths(1);
            firstInMonth = new PayPeriod(nextMonth.Year, nextMonth.Month, half);
        }

        var lastMonth = new DateTime(firstInMonth.Year, firstInMonth.Month, 1).AddMonths(termCount - 1);
        var last = new PayPeriod(lastMonth.Year, lastMonth.Month, half);
        return PeriodEnd(last);
    }

    /// <summary>
    /// Estima cuántos pagos de un préstamo ya debieron ocurrir antes del periodo actual.
    /// Se usa solo para mostrar "saldo estimado" (no para contabilidad).
    /// </summary>
    public static int EstimateLoanPaymentsDone(EmployeeDeduction d, PayPeriod current)
    {
        if (d.Type != EmployeeDeductionType.Prestamo) return 0;
        if (d.TermCount is null or <= 0) return 0;

        if (d.Frequency == EmployeeDeductionFrequency.Quincenal)
        {
            var first = FromDate(d.StartDate);
            // contamos cuántas quincenas completas pasaron antes del periodo actual
            var cur = first;
            var count = 0;
            while (cur.Year < current.Year
                   || (cur.Year == current.Year && cur.Month < current.Month)
                   || (cur.Year == current.Year && cur.Month == current.Month && cur.Half < current.Half))
            {
                count++;
                cur = Next(cur);
                if (count >= d.TermCount.Value) break;
            }
            return count;
        }

        // mensual
        var half = d.ApplyOnHalf ?? 2;
        if (current.Half != half)
        {
            // si no es la quincena de cobro, el "pago del mes" aún no ocurre
            // (contamos meses completos previos)
        }

        var firstMonth = new DateTime(d.StartDate.Year, d.StartDate.Month, 1);
        var firstPeriod = new PayPeriod(firstMonth.Year, firstMonth.Month, half);
        var firstDue = PeriodEnd(firstPeriod);
        if (d.StartDate.Date > firstDue.Date)
            firstMonth = firstMonth.AddMonths(1);

        var currentMonth = new DateTime(current.Year, current.Month, 1);
        var monthsDiff = ((currentMonth.Year - firstMonth.Year) * 12) + (currentMonth.Month - firstMonth.Month);
        if (monthsDiff < 0) monthsDiff = 0;

        // si es el mismo mes pero todavía no llegó la quincena de cobro, no cuenta como pago hecho
        if (monthsDiff == 0 && current.Half < half)
            return 0;

        var done = monthsDiff;
        if (done > d.TermCount.Value) done = d.TermCount.Value;
        return done;
    }
}
