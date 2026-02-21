using HNControl.Web.Models;

namespace HNControl.Web.Services.Monitoring;

public record ProbeResult(bool Success, int? LatencyMs, string Error);

public interface IMonitorProbeService
{
    Task<ProbeResult> ProbeAsync(MonitorTarget target, CancellationToken ct = default);
}
