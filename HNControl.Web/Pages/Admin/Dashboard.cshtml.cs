using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin;

[Authorize(Roles = AppRoles.Admin)]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DashboardModel(ApplicationDbContext db) => _db = db;

    public record KpiVm(int Employees, int OrdersInReview, int OverdueProjects, int PendingViaticWeeks);
    public KpiVm Kpi { get; set; } = new(0, 0, 0, 0);

    public record TopVm(string UserId, string Name, decimal Variable);
    public List<TopVm> Top { get; set; } = new();

    public string OrdersLabelsJson { get; set; } = "[]";
    public string OrdersValuesJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        var employees = await _db.EmployeeProfiles.CountAsync();

        var ordersInReview = await _db.ServiceOrders
            .Where(o => o.Status == ServiceOrderStatus.InReview)
            .CountAsync();

        // ✅ EstimatedEndDate es DateTime (no nullable) => QUITAMOS "!= null"
        var today = DateTime.UtcNow.Date;

        var overdueProjects = await _db.Projects
            .Where(p => p.Status == ProjectStatus.Active && p.EstimatedEndDate < today)
            .CountAsync();

        var pendingViaticWeeks = await _db.ViaticWeeks
            .Where(w => w.Status == ViaticWeekStatus.Submitted)
            .CountAsync();

        Kpi = new KpiVm(employees, ordersInReview, overdueProjects, pendingViaticWeeks);

        // Top variable (última evaluación por empleado)
        var latest = await _db.PerformanceReviews
            .GroupBy(r => r.UserId)
            .Select(g => g.OrderByDescending(x => x.PeriodStart).First())
            .ToListAsync();

        var profiles = await _db.EmployeeProfiles.ToListAsync();

        Top = latest
            .Join(profiles, r => r.UserId, e => e.UserId, (r, e) => new TopVm(e.UserId, e.FullName, r.VariablePercent))
            .OrderByDescending(x => x.Variable)
            .Take(8)
            .ToList();

        // Chart órdenes completadas últimos 30 días
        var start = today.AddDays(-29);
        var days = Enumerable.Range(0, 30).Select(i => start.AddDays(i)).ToList();

        // ✅ Usar FinalizedAt (columna real) y HasValue
        var completed = await _db.ServiceOrders
            .Where(o => (o.Status == ServiceOrderStatus.Finalized || o.Status == ServiceOrderStatus.Completed)
                        && o.FinalizedAt.HasValue
                        && o.FinalizedAt.Value.Date >= start)
            .GroupBy(o => o.FinalizedAt!.Value.Date)
            .Select(g => new { Day = g.Key, Cnt = g.Count() })
            .ToListAsync();

        var labels = days.Select(d => d.ToString("MM-dd")).ToList();
        var values = days.Select(d => completed.FirstOrDefault(x => x.Day == d)?.Cnt ?? 0).ToList();

        OrdersLabelsJson = JsonSerializer.Serialize(labels);
        OrdersValuesJson = JsonSerializer.Serialize(values);
    }
}
