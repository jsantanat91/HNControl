using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
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

    // filtros quincena
    public int Year { get; set; }
    public int Month { get; set; }
    public int Half { get; set; } // 1=1-15, 2=16-fin

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // KPI
    public int EmployeesTotal { get; set; }
    public int EmployeesRated { get; set; }
    public double? AvgScoreAll { get; set; }
    public decimal TotalPayroll80 { get; set; }
    public decimal TotalPayroll20 { get; set; }
    public decimal TotalPayrollPay { get; set; }

    // JSON para charts
    public string LabelsJson { get; set; } = "[]";
    public string TotalPayJson { get; set; } = "[]";
    public string VarPercentJson { get; set; } = "[]";

    public async Task OnGetAsync(int? year, int? month, int? half)
    {
        var now = DateTime.Today;
        Year = year ?? now.Year;
        Month = month ?? now.Month;

        var suggestedHalf = now.Day <= 15 ? 1 : 2;
        Half = half ?? suggestedHalf;
        if (Half is not (1 or 2)) Half = suggestedHalf;

        (PeriodStart, PeriodEnd) = GetQuincena(Year, Month, Half);

        var employees = await _db.EmployeeProfiles
            .OrderBy(e => e.FullName)
            .ToListAsync();

        EmployeesTotal = employees.Count;

        var reviews = await _db.PerformanceReviews
            .Where(r => r.PeriodStart == PeriodStart && r.PeriodEnd == PeriodEnd)
            .ToListAsync();

        // ✅ FIX: antes era r.EmployeeUserId, ahora es r.UserId
        var byUser = reviews.ToDictionary(r => r.UserId, r => r);

        var labels = new List<string>();
        var totalPays = new List<decimal>();
        var varPercents = new List<decimal>();

        foreach (var e in employees)
        {
            byUser.TryGetValue(e.UserId, out var r);
            var hasReview = r != null;

            var variablePercent = r?.VariablePercent ?? 0m;
            if (variablePercent < 0m) variablePercent = 0m;
            if (variablePercent > 1m) variablePercent = 1m;

            var salaryBase = e.SalaryBase;

            // 80/20 real: 80% fijo + (20% * VariablePercent)
            var fixed80 = salaryBase * 0.80m;
            var var20 = salaryBase * 0.20m * variablePercent;
            var totalPay = fixed80 + var20;

            var avgScore = hasReview ? TryGetAverageScore(r!) : null;

            Rows.Add(new Row(
                e.UserId,
                e.FullName,
                salaryBase,
                hasReview,
                variablePercent,
                var20,
                totalPay,
                avgScore
            ));

            labels.Add(e.FullName);
            totalPays.Add(totalPay);
            varPercents.Add(variablePercent * 100m); // 0..100
        }

        EmployeesRated = Rows.Count(x => x.HasReview);
        AvgScoreAll = Rows
            .Where(x => x.AvgScore.HasValue)
            .Select(x => x.AvgScore!.Value)
            .DefaultIfEmpty()
            .Average();

        TotalPayroll80 = Rows.Sum(x => x.SalaryBase * 0.80m);
        TotalPayroll20 = Rows.Sum(x => x.VariableAmount);
        TotalPayrollPay = Rows.Sum(x => x.TotalPay);

        LabelsJson = JsonSerializer.Serialize(labels);
        TotalPayJson = JsonSerializer.Serialize(totalPays);
        VarPercentJson = JsonSerializer.Serialize(varPercents);
    }

    private static (DateTime start, DateTime end) GetQuincena(int year, int month, int half)
    {
        if (half == 1)
        {
            var start = new DateTime(year, month, 1);
            var end = new DateTime(year, month, 15);
            return (start, end);
        }
        else
        {
            var start = new DateTime(year, month, 16);
            var end = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            return (start, end);
        }
    }

    // Soporta nombres viejos/nuevos sin reventar compilación
    private static double? TryGetAverageScore(PerformanceReview r)
    {
        var groups = new[]
        {
            new []{ "PersonalPerformance", "PersonalPerformanceScore" },
            new []{ "Teamwork" },
            new []{ "Punctuality", "PunctualityAttendance" },
            new []{ "ProjectExecution" },
            new []{ "OrderAndCleanliness", "OrderCleanliness", "OrderCleanlinessScore" },
            new []{ "TechnicalSkills" },
        };

        var values = new List<int>();

        foreach (var names in groups)
        {
            int? val = null;
            foreach (var name in names)
            {
                var p = r.GetType().GetProperty(name);
                if (p is null) continue;
                if (p.PropertyType != typeof(int)) continue;

                val = (int)p.GetValue(r)!;
                break;
            }

            if (!val.HasValue) return null;
            values.Add(val.Value);
        }

        return values.Average();
    }
}
