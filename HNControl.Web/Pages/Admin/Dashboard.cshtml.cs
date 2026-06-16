using System.Net;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HNControl.Web.Pages.Admin;

[Authorize(Roles = AppRoles.Admin)]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IPayrollReceiptService _payrollReceipt;
    private readonly IEmailSender _emailSender;
    private readonly IWhatsAppSender _whatsApp;
    private readonly IFileStorage _storage;
    private const string DefaultWhatsAppPayrollReceiptTemplate =
        "Hola {NombreEmpleado}, tu recibo de nomina del periodo {Periodo} esta disponible. Neto: {TotalNeto}. Ingresa al portal para consultarlo.";

    public DashboardModel(ApplicationDbContext db, IPayrollReceiptService payrollReceipt, IEmailSender emailSender, IWhatsAppSender whatsApp, IFileStorage storage)
    {
        _db = db;
        _payrollReceipt = payrollReceipt;
        _emailSender = emailSender;
        _whatsApp = whatsApp;
        _storage = storage;
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

    public record TopVm(string UserId, string Name, decimal Variable);
    public record PayrollSummaryVm(
        string UserId,
        string Name,
        bool HasPhoto,
        decimal SalaryBase,
        decimal VariablePct,
        decimal Deductions,
        decimal Bonuses,
        decimal NetEstimated,
        bool IsPaid,
        DateTime? PaidAt);

    public KpiVm Kpi { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m);
    public List<TopVm> Top { get; set; } = new();
    public List<PayrollSummaryVm> PayrollRows { get; set; } = new();

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
    public string TopVarLabelsJson { get; set; } = "[]";
    public string TopVarValuesJson { get; set; } = "[]";

    public string PayrollPeriodLabel { get; set; } = "";
    public DateTime PayrollPeriodStart { get; set; }
    public DateTime PayrollPeriodEnd { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PayrollYear { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PayrollMonth { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PayrollHalf { get; set; }

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        var (selectedYear, selectedMonth, selectedHalf, selectedStart, selectedEnd) =
            ResolveSelectedPayrollPeriod(PayrollYear, PayrollMonth, PayrollHalf);

        PayrollYear = selectedYear;
        PayrollMonth = selectedMonth;
        PayrollHalf = selectedHalf;
        PayrollPeriodStart = selectedStart;
        PayrollPeriodEnd = selectedEnd;

        var employees = await _db.EmployeeProfiles.CountAsync();
        var ordersInReview = await _db.ServiceOrders.Where(o => o.Status == ServiceOrderStatus.InReview).CountAsync();

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
            .Where(q => q.Status == QuoteRequestStatus.Accepted
                        && q.AcceptedAt.HasValue
                        && q.AcceptedAt.Value >= monthStart
                        && q.AcceptedAt.Value < monthEnd)
            .CountAsync();

        var quotesRevenueMonth = await _db.QuoteRequests
            .Where(q => q.Status == QuoteRequestStatus.Accepted
                        && q.AcceptedAt.HasValue
                        && q.AcceptedAt.Value >= monthStart
                        && q.AcceptedAt.Value < monthEnd)
            .SumAsync(q => q.EstimatedTotal ?? 0m);

        Kpi = new KpiVm(
            employees,
            ordersInReview,
            overdueProjects,
            pendingViaticWeeks,
            pendingLeaves,
            examsToGrade,
            pendingInvOrders,
            lowStockItems,
            openTickets,
            ticketSlaBreached,
            quotesAcceptedMonth,
            quotesRevenueMonth);

        await LoadPayrollSummaryAsync(selectedStart, selectedEnd);

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

        TopVarLabelsJson = JsonSerializer.Serialize(Top.Select(x => x.Name));
        TopVarValuesJson = JsonSerializer.Serialize(Top.Select(x => Math.Round(x.Variable * 100m, 2)));

        var chartStart = today.AddDays(-29);
        var days = Enumerable.Range(0, 30).Select(i => chartStart.AddDays(i)).ToList();

        var completed = await _db.ServiceOrders
            .Where(o => (o.Status == ServiceOrderStatus.Finalized || o.Status == ServiceOrderStatus.Completed)
                        && o.FinalizedAt.HasValue
                        && o.FinalizedAt.Value.Date >= chartStart)
            .GroupBy(o => o.FinalizedAt!.Value.Date)
            .Select(g => new { Day = g.Key, Cnt = g.Count() })
            .ToListAsync();

        var labels = days.Select(d => d.ToString("MM-dd")).ToList();
        var values = days.Select(d => completed.FirstOrDefault(x => x.Day == d)?.Cnt ?? 0).ToList();
        OrdersLabelsJson = JsonSerializer.Serialize(labels);
        OrdersValuesJson = JsonSerializer.Serialize(values);

        var ex = await _db.ExamAssignments
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .OrderBy(g => g.Key)
            .Select(g => new { Status = g.Key.ToString(), Cnt = g.Count() })
            .ToListAsync();

        ExamStatusLabelsJson = JsonSerializer.Serialize(ex.Select(x => x.Status));
        ExamStatusValuesJson = JsonSerializer.Serialize(ex.Select(x => x.Cnt));

        var inv = await _db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.Status == InventoryMovementStatus.Approved && m.ApprovedAt.HasValue && m.ApprovedAt.Value.Date >= chartStart)
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
                .Where(q => q.Status == QuoteRequestStatus.Accepted
                            && q.AcceptedAt.HasValue
                            && q.AcceptedAt.Value >= mStart
                            && q.AcceptedAt.Value < mEnd)
                .SumAsync(q => q.EstimatedTotal ?? 0m);
            salesLabels.Add(mStart.ToLocalTime().ToString("MMM yy"));
            salesValues.Add(sum);
        }

        QuoteSalesLabelsJson = JsonSerializer.Serialize(salesLabels);
        QuoteSalesValuesJson = JsonSerializer.Serialize(salesValues);

        var ticketsClosed = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.ClosedAt.HasValue && t.ClosedAt.Value.Date >= chartStart)
            .GroupBy(t => t.ClosedAt!.Value.Date)
            .Select(g => new { Day = g.Key, Cnt = g.Count() })
            .ToListAsync();

        var closedValues = days.Select(d => ticketsClosed.FirstOrDefault(x => x.Day == d)?.Cnt ?? 0).ToList();
        TicketsClosedLabelsJson = JsonSerializer.Serialize(labels);
        TicketsClosedValuesJson = JsonSerializer.Serialize(closedValues);
    }

        public async Task<IActionResult> OnGetPayrollPhotoAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return NotFound();

        var p = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.ProfilePhotoStoragePath, x.ProfilePhotoOriginalFileName })
            .FirstOrDefaultAsync();

        if (p == null || string.IsNullOrWhiteSpace(p.ProfilePhotoStoragePath))
            return NotFound();

        try
        {
            var name = string.IsNullOrWhiteSpace(p.ProfilePhotoOriginalFileName) ? "foto_empleado" : p.ProfilePhotoOriginalFileName;
            var (stream, contentType, _) = await _storage.OpenAsync(p.ProfilePhotoStoragePath, name);
            return File(stream, contentType);
        }
        catch
        {
            return NotFound();
        }
    }
public async Task<IActionResult> OnPostMarkPaidAsync(string userId, int? payrollYear, int? payrollMonth, int? payrollHalf)
    {
        var (selectedYear, selectedMonth, selectedHalf, selectedStart, selectedEnd) =
            ResolveSelectedPayrollPeriod(payrollYear, payrollMonth, payrollHalf);

        if (string.IsNullOrWhiteSpace(userId))
        {
            FlashError = "Falta empleado para marcar pago.";
            return RedirectToPage(new { payrollYear = selectedYear, payrollMonth = selectedMonth, payrollHalf = selectedHalf });
        }

        var employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (employee == null)
        {
            FlashError = "No se encontro el empleado.";
            return RedirectToPage(new { payrollYear = selectedYear, payrollMonth = selectedMonth, payrollHalf = selectedHalf });
        }

        if (string.IsNullOrWhiteSpace(employee.Email))
        {
            FlashError = $"El empleado {employee.FullName} no tiene correo configurado.";
            return RedirectToPage(new { payrollYear = selectedYear, payrollMonth = selectedMonth, payrollHalf = selectedHalf });
        }

        var periodStart = selectedStart;
        var periodEnd = selectedEnd;
        var payrollDate = DateTime.Now.Date;

        var dispatch = await _db.PayrollReceiptDispatches
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PeriodStart == periodStart && x.PeriodEnd == periodEnd);

        if (dispatch?.IsSent == true)
        {
            FlashSuccess = $"El pago de {employee.FullName} ya estaba confirmado para este periodo.";
            return RedirectToPage(new { payrollYear = selectedYear, payrollMonth = selectedMonth, payrollHalf = selectedHalf });
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
                dispatch.LastError = "No se pudo construir el recibo de nomina.";
                await _db.SaveChangesAsync();
                FlashError = $"No se pudo generar recibo para {employee.FullName}.";
                return RedirectToPage(new { payrollYear = selectedYear, payrollMonth = selectedMonth, payrollHalf = selectedHalf });
            }

            var pdf = _payrollReceipt.RenderPdf(data);
            var subject = $"Pago aplicado de nomina · {periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
            var body = $@"
                <p>Hola {WebUtility.HtmlEncode(data.FullName)},</p>
                <p>Te confirmamos que tu pago de nomina fue aplicado.</p>
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

            await TrySendPayrollWhatsAppAsync(employee.Phone, data);

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

        return RedirectToPage(new { payrollYear = selectedYear, payrollMonth = selectedMonth, payrollHalf = selectedHalf });
    }

    private async Task TrySendPayrollWhatsAppAsync(string? phone, PayrollReceiptData data)
    {
        if (string.IsNullOrWhiteSpace(phone) || !await _whatsApp.IsConfiguredAsync())
            return;

        try
        {
            var cfg = await LoadSystemConfigSafeAsync();
            var period = $"{data.PeriodStart:yyyy-MM-dd} a {data.PeriodEnd:yyyy-MM-dd}";
            var message = WhatsAppTemplateRenderer.Render(
                cfg?.WhatsAppPayrollReceiptTemplate,
                DefaultWhatsAppPayrollReceiptTemplate,
                new Dictionary<string, string?>
                {
                    ["NombreEmpleado"] = data.FullName,
                    ["CorreoEmpleado"] = data.Email,
                    ["Periodo"] = period,
                    ["FechaPago"] = data.PayrollDate.ToString("yyyy-MM-dd"),
                    ["TotalNeto"] = WhatsAppTemplateRenderer.Money(data.NetEstimated),
                    ["TotalBruto"] = WhatsAppTemplateRenderer.Money(data.GrossEstimated),
                    ["Deducciones"] = WhatsAppTemplateRenderer.Money(data.Deductions),
                    ["Bonos"] = WhatsAppTemplateRenderer.Money(data.Bonuses),
                    ["UrlPortal"] = cfg?.PublicBaseUrl
                });

            await _whatsApp.SendAsync(phone, message);
        }
        catch
        {
            // El recibo por correo no debe fallar si el gateway WA no esta disponible.
        }
    }

    private async Task<SystemConfiguration?> LoadSystemConfigSafeAsync()
    {
        try
        {
            return await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            return null;
        }
    }
    private async Task LoadPayrollSummaryAsync(DateTime periodStart, DateTime periodEnd)
    {
        PayrollPeriodLabel = $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
        PayrollPeriodStart = periodStart;
        PayrollPeriodEnd = periodEnd;

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
            var paid = dispatches.TryGetValue(e.UserId, out var dispatch) && dispatch.IsSent;
            var paidAt = paid ? dispatch.SentAt : null;

            rows.Add(new PayrollSummaryVm(
                e.UserId,
                e.FullName,
                !string.IsNullOrWhiteSpace(e.ProfilePhotoStoragePath),
                e.SalaryBase,
                vp,
                deductions,
                bonuses,
                net,
                paid,
                paidAt));
        }

        PayrollRows = rows.OrderByDescending(x => x.NetEstimated).Take(60).ToList();
    }

    private static (int year, int month, int half, DateTime start, DateTime end) ResolveSelectedPayrollPeriod(int? year, int? month, int? half)
    {
        var now = DateTime.Now.Date;
        var y = year.GetValueOrDefault(now.Year);
        var m = month.GetValueOrDefault(now.Month);
        var h = half.GetValueOrDefault(now.Day <= 15 ? 1 : 2);

        if (y < 2020 || y > 2100) y = now.Year;
        if (m < 1 || m > 12) m = now.Month;
        if (h is not (1 or 2)) h = (now.Day <= 15 ? 1 : 2);

        var start = h == 1 ? new DateTime(y, m, 1) : new DateTime(y, m, 16);
        var end = h == 1 ? new DateTime(y, m, 15) : new DateTime(y, m, DateTime.DaysInMonth(y, m));
        return (y, m, h, start, end);
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




