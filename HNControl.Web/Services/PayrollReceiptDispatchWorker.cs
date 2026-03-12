using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class PayrollReceiptDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PayrollReceiptDispatchWorker> _logger;

    public PayrollReceiptDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<PayrollReceiptDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayrollReceiptDispatchWorker error");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        var tz = ResolveTimezone();
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var today = nowLocal.Date;
        var day = today.Day;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var secondPayrollDay = Math.Min(30, daysInMonth);

        if (day != 15 && day != secondPayrollDay)
            return;

        var (start, end) = day == 15
            ? (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 15))
            : (new DateTime(today.Year, today.Month, 16), new DateTime(today.Year, today.Month, secondPayrollDay));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var receipt = scope.ServiceProvider.GetRequiredService<IPayrollReceiptService>();

        var employees = await db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive && x.Email != null && x.Email != "")
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);

        foreach (var emp in employees)
        {
            var log = await db.PayrollReceiptDispatches
                .FirstOrDefaultAsync(x =>
                    x.UserId == emp.UserId &&
                    x.PeriodStart == start &&
                    x.PeriodEnd == end, ct);

            if (log?.IsSent == true)
                continue;

            if (log?.LastAttemptAt != null && (DateTime.UtcNow - log.LastAttemptAt.Value).TotalMinutes < 20)
                continue;

            if (log == null)
            {
                log = new PayrollReceiptDispatch
                {
                    UserId = emp.UserId,
                    RecipientEmail = emp.Email.Trim(),
                    PeriodStart = start,
                    PeriodEnd = end,
                    PayrollDate = today
                };
                db.PayrollReceiptDispatches.Add(log);
            }
            else
            {
                log.RecipientEmail = emp.Email.Trim();
                log.PayrollDate = today;
            }

            log.AttemptCount += 1;
            log.LastAttemptAt = DateTime.UtcNow;

            try
            {
                var data = await receipt.BuildAsync(emp.UserId, start, end, today);
                if (data == null)
                {
                    log.LastError = "No se pudo construir recibo para empleado.";
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var pdf = receipt.RenderPdf(data);
                var subject = $"Recibo de nomina {start:yyyy-MM-dd} a {end:yyyy-MM-dd}";
                var body = $@"
                    <p>Hola {System.Net.WebUtility.HtmlEncode(data.FullName)},</p>
                    <p>Adjuntamos tu recibo de nomina del periodo <b>{start:yyyy-MM-dd}</b> al <b>{end:yyyy-MM-dd}</b>.</p>
                    <p>Neto estimado: <b>{data.NetEstimated:C2}</b></p>
                    <p>Equipo HN Control</p>";

                await email.SendAsync(
                    data.Email.Trim(),
                    subject,
                    body,
                    pdf,
                    $"recibo_nomina_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf",
                    "application/pdf");

                log.IsSent = true;
                log.SentAt = DateTime.UtcNow;
                log.LastError = null;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                log.LastError = ex.Message.Length > 1100 ? ex.Message[..1100] : ex.Message;
                await db.SaveChangesAsync(ct);
                _logger.LogWarning(ex, "No se pudo enviar recibo de nomina a {Email}", emp.Email);
            }
        }
    }

    private static TimeZoneInfo ResolveTimezone()
    {
        var candidates = new[]
        {
            "America/Mexico_City",
            "Central Standard Time (Mexico)",
            "Central Standard Time"
        };

        foreach (var id in candidates)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        }
        return TimeZoneInfo.Local;
    }
}

