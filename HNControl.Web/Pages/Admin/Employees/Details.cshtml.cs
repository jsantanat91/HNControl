using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public EmployeeProfile? Employee { get; set; }

    public record KpiVm(int CompletedOrders30d, int InReviewOrders, int ActiveProjects, int OverdueProjects, decimal WeeklyViaticTotal);
    public KpiVm Kpi { get; set; } = new(0, 0, 0, 0, 0m);

    public PerformanceReview? LastReview { get; set; }
    public string LastReviewPeriod { get; set; } = "";

    public record PayVm(decimal Fixed80, decimal VariableMoney, decimal Total);
    public PayVm Pay { get; set; } = new(0m, 0m, 0m);

    public string ChartLabelsJson { get; set; } = "[]";
    public string ChartValuesJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (Employee == null) return Page();

        var today = DateTime.UtcNow.Date;
        var start30 = today.AddDays(-29);

        // Orders KPIs (30d)
        var completedOrders30d = await _db.ServiceOrders
            .Where(o => o.AssignedUserId == userId
                        && (o.Status == ServiceOrderStatus.Finalized || o.Status == ServiceOrderStatus.Completed)
                        && o.FinalizedAt.HasValue
                        && o.FinalizedAt.Value.Date >= start30)
            .CountAsync();

        var inReviewOrders = await _db.ServiceOrders
            .Where(o => o.AssignedUserId == userId && o.Status == ServiceOrderStatus.InReview)
            .CountAsync();

        // Projects KPIs
        var activeProjects = await _db.Projects
            .Where(p => p.AssignedUserId == userId && p.Status == ProjectStatus.Active)
            .CountAsync();

        // ✅ EstimatedEndDate NO es nullable => no compares con null
        var overdueProjects = await _db.Projects
            .Where(p => p.AssignedUserId == userId && p.Status == ProjectStatus.Active && p.EstimatedEndDate < today)
            .CountAsync();

        // Viáticos semana actual
        var weekStart = GetWeekStartUtc(today);
        var weeklyViaticTotal = await _db.ViaticWeeks
            .Where(w => w.UserId == userId && w.WeekStartDate == weekStart)
            .Select(w => w.TotalAmount)
            .FirstOrDefaultAsync();

        Kpi = new KpiVm(completedOrders30d, inReviewOrders, activeProjects, overdueProjects, weeklyViaticTotal);

        // Última evaluación (para 80/20)
        LastReview = await _db.PerformanceReviews
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PeriodStart)
            .FirstOrDefaultAsync();

        if (LastReview != null)
        {
            LastReviewPeriod = $"{LastReview.PeriodStart:yyyy-MM-dd} → {LastReview.PeriodEnd:yyyy-MM-dd}";

            var baseQuincenal = Employee.SalaryBase / 2m;            // suponiendo SalaryBase mensual
            var fixed80 = baseQuincenal * 0.80m;
            var varMax20 = baseQuincenal * 0.20m;
            var varMoney = varMax20 * LastReview.VariablePercent;

            Pay = new PayVm(fixed80, varMoney, fixed80 + varMoney);
        }
        else
        {
            Pay = new PayVm(0m, 0m, (Employee.SalaryBase / 2m) * 0.80m);
        }

        var last12 = await _db.PerformanceReviews
      .Where(r => r.UserId == userId)
      .OrderByDescending(r => r.PeriodStart)
      .Take(12)
      .ToListAsync();

        last12.Reverse(); // ahora queda ascendente para la gráfica

        var labels = last12.Select(r => r.PeriodStart.ToString("MM-dd")).ToList();
        var values = last12.Select(r => r.VariablePercent).ToList();

        ChartLabelsJson = JsonSerializer.Serialize(labels);
        ChartValuesJson = JsonSerializer.Serialize(values);

        return Page();
    }

    private static DateTime GetWeekStartUtc(DateTime utcDate)
    {
        // Lunes como inicio de semana
        var d = utcDate;
        while (d.DayOfWeek != DayOfWeek.Monday)
            d = d.AddDays(-1);

        return d.Date;
    }
}
