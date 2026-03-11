using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HNControl.Web.Services.Monitoring;

/// <summary>
/// Worker de monitoreo: revisa targets activos y guarda historial.
/// </summary>
public class MonitorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitorWorker> _log;

    public MonitorWorker(IServiceScopeFactory scopeFactory, ILogger<MonitorWorker> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // loop simple: cada 5s revisa qué targets ya les toca
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MonitorWorker tick falló");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var probe = scope.ServiceProvider.GetRequiredService<IMonitorProbeService>();
        var ticketFlow = scope.ServiceProvider.GetRequiredService<ITicketFlowService>();

        var now = DateTime.UtcNow;

        // Tomamos lote chico para no monopolizar DB
        var targets = await db.MonitorTargets
            .Where(t => t.IsActive && (t.NextCheckAt == null || t.NextCheckAt <= now))
            .OrderBy(t => t.NextCheckAt)
            .Take(30)
            .ToListAsync(ct);

        if (targets.Count == 0)
            return;

        foreach (var t in targets)
        {
            ct.ThrowIfCancellationRequested();

            var wasDown = t.LastStatus == MonitorStatus.Down;
            var res = await probe.ProbeAsync(t, ct);

            // historial
            db.MonitorChecks.Add(new MonitorCheck
            {
                TargetId = t.Id,
                CheckedAt = now,
                Success = res.Success,
                LatencyMs = res.LatencyMs,
                Error = res.Error ?? ""
            });

            t.LastCheckedAt = now;
            t.LastLatencyMs = res.LatencyMs;
            t.LastError = res.Success ? "" : (res.Error ?? "");

            if (res.Success)
            {
                t.ConsecutiveFails = 0;
                t.LastStatus = MonitorStatus.Up;
            }
            else
            {
                t.ConsecutiveFails += 1;
                if (t.ConsecutiveFails >= Math.Max(1, t.FailThreshold))
                    t.LastStatus = MonitorStatus.Down;
            }

            t.NextCheckAt = now.AddSeconds(Math.Max(10, t.CheckIntervalSeconds));
            t.UpdatedAt = now;

            // Auto-ticket por caída (solo al transicionar a DOWN).
            if (!wasDown && t.LastStatus == MonitorStatus.Down)
            {
                var title = $"Caida monitor: {t.Name}";
                var host = !string.IsNullOrWhiteSpace(t.IpAddress) ? t.IpAddress : t.Fqdn;
                var desc = $"Monitoreo detecto falla en {t.Name} ({host}). Error: {t.LastError}";
                await ticketFlow.CreateMonitoringAutoAsync(t.Id, title, desc, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
