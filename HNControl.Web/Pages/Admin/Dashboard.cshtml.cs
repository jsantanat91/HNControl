using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin;

[Authorize(Roles = AppRoles.Admin)]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IPayrollReceiptService _payrollReceipt;
    private readonly IEmailSender _emailSender;
    public DashboardModel(ApplicationDbContext db, IPayrollReceiptService payrollReceipt, IEmailSender emailSender)
    {
        _db = db;
        _payrollReceipt = payrollReceipt;
        _emailSender = emailSender;
    }

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
        int TicketSlaBreached,
        int QuotesAcceptedMonth,
        decimal QuotesRevenueMonth
    );

    public KpiVm Kpi { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m);

    public record TopVm(string UserId, string Name, decimal Variable);
    public List<TopVm> Top { get; set; } = new();

    public string OrdersLabelsJson { get; set; } = "[]";
    public string OrdersValuesJson { get; set; } = "[]";

    public string ExamStatusLabelsJson { get; set; } = "[]";
    public string ExamStatusValuesJson { get; set; } = "[]";

    public string InvLabelsJson { get; set; } = "[]";
    public string InvInValuesJson { get; set; } = "[]";
    public string InvOutValuesJson { get; set; } = "[]";
    public string QuoteSalesLabelsJson { get; set; } = "[]";
    public string QuoteSalesValuesJson { get; set; } = "[]";
    public string TicketsClosedLabelsJson { get; set; } = "[]";
    public string TicketsClosedValuesJson { get; set; } = "[]";
    public record PayrollSummaryVm(string UserId, string Name, decimal SalaryBase, decimal VariablePct, decimal Deductions, decimal Bonuses, decimal NetEstimated, bool IsPaid, DateTime? PaidAt);
    public List<PayrollSummaryVm> PayrollRows { get; set; } = new();
    public string PayrollPeriodLabel { get; set; } = "";
    [TempData]
    public string? FlashSuccess { get; set; }
    [TempData]
    public string? FlashError { get; set; }

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

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var quotesAcceptedMonth = await _db.QuoteRequests
            .Where(q => q.Status == QuoteRequestStatus.Accepted && q.AcceptedAt.HasValue && q.AcceptedAt.Value >= monthStart && q.AcceptedAt.Value < monthEnd)
            .CountAsync();
        var quotesRevenueMonth = await _db.QuoteRequests
            .Where(q => q.Status == QuoteRequestStatus.Accepted && q.AcceptedAt.HasValue && q.AcceptedAt.Value >= monthStart && q.AcceptedAt.Value < monthEnd)
            .SumAsync(q => q.EstimatedTotal ?? 0m);

        Kpi = new KpiVm(employees, ordersInReview, overdueProjects, pendingViaticWeeks, pendingLeaves, examsToGrade, pendingInvOrders, lowStockItems, openTickets, ticketSlaBreached, quotesAcceptedMonth, quotesRevenueMonth);
        await LoadPayrollSummaryAsync();

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

        var salesLabels = new List<string>();
        var salesValues = new List<decimal>();
        for (var i = 5; i >= 0; i--)
        {
            var mStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var mEnd = mStart.AddMonths(1);
            var sum = await _db.QuoteRequests
                .Where(q => q.Status == QuoteRequestStatus.Accepted && q.AcceptedAt.HasValue && q.AcceptedAt.Value >= mStart && q.AcceptedAt.Value < mEnd)
                .SumAsync(q => q.EstimatedTotal ?? 0m);
            salesLabels.Add(mStart.ToLocalTime().ToString("MMM yy"));
            salesValues.Add(sum);
        }

        QuoteSalesLabelsJson = JsonSerializer.Serialize(salesLabels);
        QuoteSalesValuesJson = JsonSerializer.Serialize(salesValues);

        var ticketsClosed = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.ClosedAt.HasValue && t.ClosedAt.Value.Date >= start)
            .GroupBy(t => t.ClosedAt!.Value.Date)
            .Select(g => new { Day = g.Key, Cnt = g.Count() })
            .ToListAsync();
        var closedValues = days.Select(d => ticketsClosed.FirstOrDefault(x => x.Day == d)?.Cnt ?? 0).ToList();
        TicketsClosedLabelsJson = JsonSerializer.Serialize(labels);
        TicketsClosedValuesJson = JsonSerializer.Serialize(closedValues);
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            FlashError = "Falta empleado para marcar pago.";
            return RedirectToPage();
        }

        var employee = await _db.EmployeeProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (employee == null)
        {
            FlashError = "No se encontró el empleado.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(employee.Email))
        {
            FlashError = $"El empleado {employee.FullName} no tiene correo configurado.";
            return RedirectToPage();
        }

        var (periodStart, periodEnd) = ResolveCurrentPeriodUtc();
        var payrollDate = DateTime.Now.Date;

        var dispatch = await _db.PayrollReceiptDispatches
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PeriodStart == periodStart && x.PeriodEnd == periodEnd);

        if (dispatch?.IsSent == true)
        {
            FlashSuccess = $"El pago de {employee.FullName} ya estaba confirmado para esta quincena.";
            return RedirectToPage();
        }

        if (dispatch == null)
        {
            dispatch = new PayrollReceiptDispatch
            {
                UserId = userId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };
            _db.PayrollReceiptDispatches.Add(dispatch);
        }

        dispatch.RecipientEmail = employee.Email.Trim();
        dispatch.PayrollDate = payrollDate;
        dispatch.AttemptCount += 1;
        dispatch.LastAttemptAt = DateTime.UtcNow;

        try
        {
            var data = await _payrollReceipt.BuildAsync(userId, periodStart, periodEnd, payrollDate);
            if (data == null)
            {
                dispatch.LastError = "No se pudo construir el recibo de nómina.";
                await _db.SaveChangesAsync();
                FlashError = $"No se pudo generar recibo para {employee.FullName}.";
                return RedirectToPage();
            }

            var pdf = _payrollReceipt.RenderPdf(data);
            var subject = $"Pago aplicado de nómina · {periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
            var body = $@"
                <p>Hola {WebUtility.HtmlEncode(data.FullName)},</p>
                <p>Te confirmamos que tu pago de nómina fue aplicado.</p>
                <p><b>Periodo:</b> {periodStart:yyyy-MM-dd} al {periodEnd:yyyy-MM-dd}<br/>
                <b>Importe pagado:</b> {data.NetEstimated:C2}<br/>
                <b>Fecha de pago:</b> {payrollDate:yyyy-MM-dd}</p>
                <p>Adjuntamos tu recibo en PDF para referencia.</p>
                <p>Equipo HN Control</p>";

            await _emailSender.SendAsync(
                data.Email.Trim(),
                subject,
                body,
                pdf,
                $"recibo_nomina_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.pdf",
                "application/pdf");

            dispatch.IsSent = true;
            dispatch.SentAt = DateTime.UtcNow;
            dispatch.LastError = null;
            await _db.SaveChangesAsync();

            FlashSuccess = $"Pago confirmado y correo enviado a {employee.FullName} ({data.Email}).";
        }
        catch (Exception ex)
        {
            dispatch.LastError = ex.Message.Length > 1100 ? ex.Message[..1100] : ex.Message;
            await _db.SaveChangesAsync();
            FlashError = $"No se pudo enviar el correo de pago para {employee.FullName}. {dispatch.LastError}";
        }

        return RedirectToPage();
    }

    private async Task LoadPayrollSummaryAsync()
    {
        var (periodStart, periodEnd) = ResolveCurrentPeriodUtc();
        PayrollPeriodLabel = $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";

        var emps = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var latest = await _db.PerformanceReviews
            .AsNoTracking()
            .GroupBy(r => r.UserId)
            .Select(g => g.OrderByDescending(x => x.PeriodStart).ThenByDescending(x => x.UpdatedAt).First())
            .ToListAsync();

        var dispatches = await _db.PayrollReceiptDispatches
            .AsNoTracking()
            .Where(x => x.PeriodStart == periodStart && x.PeriodEnd == periodEnd)
            .ToDictionaryAsync(x => x.UserId, x => x);

        var latestMap = latest.ToDictionary(x => x.UserId, x => x.VariablePercent);
        var rows = new List<PayrollSummaryVm>();

        foreach (var e in emps)
        {
            var vp = latestMap.TryGetValue(e.UserId, out var v) ? v : 0m;
            if (vp < 0m) vp = 0m;
            if (vp > 1m) vp = 1m;

            var baseQ = e.SalaryBase / 2m;
            var total = Math.Round((baseQ * 0.80m) + (baseQ * 0.20m * vp), 2);
            var (deductions, bonuses) = await CalcPayrollAdjustmentsAsync(e.UserId, baseQ, total, periodStart, periodEnd);
            var net = Math.Max(0m, Math.Round(total - deductions + bonuses, 2));
            var paid = dispatches.TryGetValue(e.UserId, out var d) && d.IsSent;
            var paidAt = paid ? d.SentAt : null;

            rows.Add(new PayrollSummaryVm(e.UserId, e.FullName, e.SalaryBase, vp, deductions, bonuses, net, paid, paidAt));
        }

        PayrollRows = rows.OrderByDescending(x => x.NetEstimated).Take(40).ToList();
    }

    private static (DateTime start, DateTime end) ResolveCurrentPeriodUtc()
    {
        var now = DateTime.Now.Date;
        if (now.Day <= 15)
            return (new DateTime(now.Year, now.Month, 1),
                    new DateTime(now.Year, now.Month, 15));

        return (new DateTime(now.Year, now.Month, 16),
                new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)));
    }

    private async Task<(decimal deductions, decimal bonuses)> CalcPayrollAdjustmentsAsync(
        string userId,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodStart,
        DateTime periodEnd)
    {
        try
        {
            var active = await _db.EmployeeDeductions
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Where(d => d.StartDate <= periodEnd && (d.EndDate == null || d.EndDate >= periodStart))
                .ToListAsync();

            var result = PayrollDeductionMath.CalculateTotals(active, baseQuincenal, estimatedQuincenal, periodStart, periodEnd);
            return (result.deductions, result.bonuses);
        }
        catch
        {
            return (0m, 0m);
        }
    }
}
