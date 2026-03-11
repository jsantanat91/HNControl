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

    public record KpiVm(
        int Employees,
        int OrdersInReview,
        int OverdueProjects,
        int PendingViaticWeeks,
        int PendingLeaveRequests,
        int ExamsToGrade,
        int PendingInventoryOrders,
        int LowStockItems,
        int OpenTickets,
        int TicketSlaBreached
    );

    public KpiVm Kpi { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public record TopVm(string UserId, string Name, decimal Variable);
    public List<TopVm> Top { get; set; } = new();

    public string OrdersLabelsJson { get; set; } = "[]";
    public string OrdersValuesJson { get; set; } = "[]";

    public string ExamStatusLabelsJson { get; set; } = "[]";
    public string ExamStatusValuesJson { get; set; } = "[]";

    public string InvLabelsJson { get; set; } = "[]";
    public string InvInValuesJson { get; set; } = "[]";
    public string InvOutValuesJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        var employees = await _db.EmployeeProfiles.CountAsync();

        var ordersInReview = await _db.ServiceOrders
            .Where(o => o.Status == ServiceOrderStatus.InReview)
            .CountAsync();

        var today = DateTime.UtcNow.Date;

        var overdueProjects = await _db.Projects
            .Where(p => p.Status == ProjectStatus.Active && p.EstimatedEndDate < today)
            .CountAsync();

        var pendingViaticWeeks = await _db.ViaticWeeks
            .Where(w => w.Status == ViaticWeekStatus.Submitted)
            .CountAsync();

        var pendingLeaves = await _db.LeaveRequests
            .Where(x => x.Status == LeaveRequestStatus.Pending)
            .CountAsync();

        var examsToGrade = await _db.ExamAssignments
            .Where(x => x.Status == ExamAssignmentStatus.Submitted)
            .CountAsync();

        // Inventario: órdenes pendientes (por lote)
        var pendingInvOrders = await _db.InventoryMovements
            .Where(m => m.Status == InventoryMovementStatus.Pending)
            .Select(m => new { m.RequestedAt, m.RequestedByUserId, m.Type, m.ProjectId, m.ResponsibleUserId })
            .Distinct()
            .CountAsync();

        var lowStockItems = await _db.InventoryItems
            .Where(i => i.IsActive && i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel)
            .CountAsync();

        var now = DateTime.UtcNow;
        var openTickets = await _db.Tickets
            .Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled)
            .CountAsync();

        var ticketSlaBreached = await _db.Tickets
            .Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled)
            .Where(t => t.SlaBreachedResponse || t.SlaBreachedResolution
                        || (t.FirstResponseAt == null && t.SlaResponseDueAt < now)
                        || (t.ResolvedAt == null && t.SlaResolutionDueAt < now))
            .CountAsync();

        Kpi = new KpiVm(employees, ordersInReview, overdueProjects, pendingViaticWeeks, pendingLeaves, examsToGrade, pendingInvOrders, lowStockItems, openTickets, ticketSlaBreached);

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

        // Chart exámenes (estatus)
        var ex = await _db.ExamAssignments
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .OrderBy(g => g.Key)
            .Select(g => new { Status = g.Key.ToString(), Cnt = g.Count() })
            .ToListAsync();

        ExamStatusLabelsJson = JsonSerializer.Serialize(ex.Select(x => x.Status));
        ExamStatusValuesJson = JsonSerializer.Serialize(ex.Select(x => x.Cnt));

        // Chart inventario (aprobados) últimos 30 días
        var inv = await _db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.Status == InventoryMovementStatus.Approved && m.ApprovedAt.HasValue && m.ApprovedAt.Value.Date >= start)
            .GroupBy(m => new { Day = m.ApprovedAt!.Value.Date, m.Type })
            .Select(g => new { g.Key.Day, g.Key.Type, Cnt = g.Count() })
            .ToListAsync();

        var invIn = days.Select(d => inv.FirstOrDefault(x => x.Day == d && x.Type == InventoryMovementType.In)?.Cnt ?? 0).ToList();
        var invOut = days.Select(d => inv.FirstOrDefault(x => x.Day == d && x.Type == InventoryMovementType.Out)?.Cnt ?? 0).ToList();

        InvLabelsJson = JsonSerializer.Serialize(labels);
        InvInValuesJson = JsonSerializer.Serialize(invIn);
        InvOutValuesJson = JsonSerializer.Serialize(invOut);
    }
}
