using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DashboardModel(ApplicationDbContext db) => _db = db;

    public record Row(
        string UserId,
        string FullName,
        decimal SalaryBase,
        bool HasReview,
        decimal VariablePercent,
        decimal VariableAmount,
        decimal TotalPay,
        double? AvgScore
    );

    public List<Row> Rows { get; set; } = new();

    public int Year { get; set; }
    public int Month { get; set; }
    public int Half { get; set; } // 1=1-15, 2=16-fin

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public int EmployeesTotal { get; set; }
    public int EmployeesRated { get; set; }
    public double? AvgScoreAll { get; set; }
    public decimal TotalPayroll80 { get; set; }
    public decimal TotalPayroll20 { get; set; }
    public decimal TotalPayrollPay { get; set; }

    public string LabelsJson { get; set; } = "[]";
    public string TotalPayJson { get; set; } = "[]";
    public string VarPercentJson { get; set; } = "[]";

    public async Task OnGetAsync(int? year, int? month, int? half)
    {
        var now = DateTime.Now;
        Year = year ?? now.Year;
        Month = month ?? now.Month;
        Half = half ?? (now.Day <= 15 ? 1 : 2);
        if (Half is not (1 or 2)) Half = (now.Day <= 15 ? 1 : 2);

        (PeriodStart, PeriodEnd) = GetQuincenaUtc(Year, Month, Half);

        // ✅ SIN filtro IsActive (porque tus filas viejas quedaron false)
        var employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();

        EmployeesTotal = employees.Count;

        var reviews = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.PeriodStart >= PeriodStart && r.PeriodStart < PeriodStart.AddDays(1)
                     && r.PeriodEnd >= PeriodEnd && r.PeriodEnd < PeriodEnd.AddDays(1))
            .ToListAsync();

        var byUser = reviews.ToDictionary(r => r.UserId, r => r);

        var labels = new List<string>();
        var totalPays = new List<decimal>();
        var varPercents = new List<decimal>();

        TotalPayroll80 = 0m;
        TotalPayroll20 = 0m;
        TotalPayrollPay = 0m;

        foreach (var e in employees)
        {
            byUser.TryGetValue(e.UserId, out var r);
            var hasReview = r != null;

            // SalaryBase lo tratamos como MENSUAL (quincena = /2)
            var baseQuincena = e.SalaryBase / 2m;
            var fijo80 = baseQuincena * 0.80m;
            var max20 = baseQuincena * 0.20m;

            var variablePercent = r?.VariablePercent ?? 0m;
            if (variablePercent < 0m) variablePercent = 0m;
            if (variablePercent > 1m) variablePercent = 1m;

            var variableMoney = max20 * variablePercent;
            var totalPay = fijo80 + variableMoney;

            var avgScore = hasReview ? TryGetAverageScore(r!) : null;

            Rows.Add(new Row(
                e.UserId,
                e.FullName,
                e.SalaryBase,
                hasReview,
                variablePercent,
                variableMoney,
                totalPay,
                avgScore
            ));

            labels.Add(e.FullName);
            totalPays.Add(decimal.Round(totalPay, 2));
            varPercents.Add(decimal.Round(variablePercent * 100m, 2));

            TotalPayroll80 += fijo80;
            TotalPayroll20 += variableMoney;
            TotalPayrollPay += totalPay;
        }

        EmployeesRated = Rows.Count(x => x.HasReview);
        AvgScoreAll = Rows.Where(x => x.AvgScore.HasValue).Select(x => x.AvgScore!.Value).DefaultIfEmpty().Average();

        LabelsJson = JsonSerializer.Serialize(labels);
        TotalPayJson = JsonSerializer.Serialize(totalPays);
        VarPercentJson = JsonSerializer.Serialize(varPercents);
    }

    private static (DateTime start, DateTime end) GetQuincenaUtc(int year, int month, int half)
    {
        if (half == 1)
            return (TimeUtil.UtcDate(new DateTime(year, month, 1)),
                    TimeUtil.UtcDate(new DateTime(year, month, 15)));

        return (TimeUtil.UtcDate(new DateTime(year, month, 16)),
                TimeUtil.UtcDate(new DateTime(year, month, DateTime.DaysInMonth(year, month))));
    }

    private static double? TryGetAverageScore(PerformanceReview r)
    {
        var values = new[]
        {
            r.PersonalPerformance,
            r.Teamwork,
            r.PunctualityAttendance,
            r.ProjectExecution,
            r.OrderCleanliness,
            r.TechnicalSkills
        };

        return values.Average();
    }
}
