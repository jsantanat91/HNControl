using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class CommercialReminderWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<CommercialReminderWorker> _log;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(45);

    public CommercialReminderWorker(IServiceProvider sp, ILogger<CommercialReminderWorker> log)
    {
        _sp = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CommercialReminderWorker failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var tpl = scope.ServiceProvider.GetRequiredService<IEventEmailTemplateService>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var today = DateTime.UtcNow.Date;
        const string reminderType = "commercial.daily";

        var alreadySent = await db.AutomationReminderLogs
            .AsNoTracking()
            .AnyAsync(x => x.ReminderType == reminderType && x.LogDate == today, ct);
        if (alreadySent) return;

        var contractPending = await db.SalesOpportunities.AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Where(x => x.WorkflowStage == SalesWorkflowStage.Contract || x.WorkflowStage == SalesWorkflowStage.Signature)
            .OrderBy(x => x.UpdatedAt)
            .Take(40)
            .ToListAsync(ct);

        var invoicePending = await db.BillingInvoiceRuns.AsNoTracking()
            .Include(x => x.Plan!).ThenInclude(x => x.Client)
            .Where(x => x.Status == BillingRunStatus.Scheduled && x.ScheduledFor <= today.AddDays(3))
            .OrderBy(x => x.ScheduledFor)
            .Take(60)
            .ToListAsync(ct);

        var commissionPending = await db.SalesOpportunities.AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Where(x => x.WorkflowStage == SalesWorkflowStage.Commission && !x.BonusDeductionId.HasValue)
            .OrderBy(x => x.UpdatedAt)
            .Take(40)
            .ToListAsync(ct);

        if (contractPending.Count == 0 && invoicePending.Count == 0 && commissionPending.Count == 0)
        {
            db.AutomationReminderLogs.Add(new AutomationReminderLog
            {
                ReminderType = reminderType,
                LogDate = today,
                SentAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return;
        }

        var html = BuildDigestHtml(contractPending, invoicePending, commissionPending);
        var defaultSubj = $"Recordatorio comercial {today:yyyy-MM-dd}";
        var (subject, body) = await tpl.RenderAsync(
            "commercial.daily.reminder",
            defaultSubj,
            html,
            new Dictionary<string, string>
            {
                ["Fecha"] = today.ToString("yyyy-MM-dd"),
                ["ContratosPendientes"] = contractPending.Count.ToString(),
                ["FacturasPendientes"] = invoicePending.Count.ToString(),
                ["ComisionesPendientes"] = commissionPending.Count.ToString()
            });

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var admin = (cfg["SeedAdmin:Email"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(admin)) recipients.Add(admin);
        foreach (var e in (cfg["Quotes:InternalCopyEmail"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            recipients.Add(e);

        foreach (var to in recipients)
            await email.SendAsync(to, subject, body);

        db.AutomationReminderLogs.Add(new AutomationReminderLog
        {
            ReminderType = reminderType,
            LogDate = today,
            SentAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static string BuildDigestHtml(
        List<SalesOpportunity> contracts,
        List<BillingInvoiceRun> invoices,
        List<SalesOpportunity> commissions)
    {
        var lines = new List<string>
        {
            "<h3>Recordatorio comercial</h3>",
            $"<p><b>Contratos/firma pendientes:</b> {contracts.Count}<br/>",
            $"<b>Facturas por vencer:</b> {invoices.Count}<br/>",
            $"<b>Comisiones pendientes:</b> {commissions.Count}</p>"
        };

        if (contracts.Count > 0)
        {
            lines.Add("<h4>Contratos/Firma</h4><ul>");
            foreach (var x in contracts.Take(8))
                lines.Add($"<li>{x.QuoteRequest?.Folio ?? "-"} - {x.QuoteRequest?.CustomerName ?? "-"} - etapa {x.WorkflowStage}</li>");
            lines.Add("</ul>");
        }

        if (invoices.Count > 0)
        {
            lines.Add("<h4>Facturas</h4><ul>");
            foreach (var x in invoices.Take(8))
                lines.Add($"<li>{x.Plan?.Client?.Name ?? "-"} - {x.PeriodLabel} - vence {x.ScheduledFor:yyyy-MM-dd}</li>");
            lines.Add("</ul>");
        }

        if (commissions.Count > 0)
        {
            lines.Add("<h4>Comisiones</h4><ul>");
            foreach (var x in commissions.Take(8))
                lines.Add($"<li>{x.QuoteRequest?.Folio ?? "-"} - {x.QuoteRequest?.CustomerName ?? "-"} - {x.CommissionAmount:C2}</li>");
            lines.Add("</ul>");
        }

        lines.Add("<p>HN Control - Automatizacion comercial</p>");
        return string.Join(Environment.NewLine, lines);
    }
}
