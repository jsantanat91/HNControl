using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HNControl.Web.Models;
using Microsoft.Extensions.Logging;

namespace HNControl.Web.Services.Monitoring;

public class MonitorProbeService : IMonitorProbeService
{
    private readonly ILogger<MonitorProbeService> _log;
    private readonly IHttpClientFactory _httpClientFactory;

    public MonitorProbeService(ILogger<MonitorProbeService> log, IHttpClientFactory httpClientFactory)
    {
        _log = log;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ProbeResult> ProbeAsync(MonitorTarget target, CancellationToken ct = default)
    {
        // 1) Determina el "host" a probar
        var host = !string.IsNullOrWhiteSpace(target.IpAddress)
            ? target.IpAddress.Trim()
            : target.Fqdn.Trim();

        if (string.IsNullOrWhiteSpace(host))
            return new ProbeResult(false, null, "Sin IP o FQDN.");

        try
        {
            switch (target.ProbeType)
            {
                case MonitorProbeType.IcmpPing:
                    return await IcmpPingAsync(host, target.TimeoutMs, ct);

                case MonitorProbeType.TcpConnect:
                    return await TcpAsync(host, target.TcpPort ?? 80, target.TimeoutMs, ct);

                case MonitorProbeType.HttpGet:
                    return await HttpAsync(target, target.TimeoutMs, ct);

                default:
                    return new ProbeResult(false, null, "Tipo de prueba no soportado.");
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Probe falló para {Host}", host);
            return new ProbeResult(false, null, ex.Message);
        }
    }

    private static async Task<ProbeResult> IcmpPingAsync(string host, int timeoutMs, CancellationToken ct)
    {
        // Nota: en Linux puede requerir permisos (CAP_NET_RAW). Si no se puede, se captura y se refleja el error.
        using var ping = new Ping();
        var sw = Stopwatch.StartNew();
        var reply = await ping.SendPingAsync(host, timeoutMs);
        sw.Stop();

        if (reply.Status == IPStatus.Success)
            return new ProbeResult(true, (int)sw.ElapsedMilliseconds, "");

        return new ProbeResult(false, null, reply.Status.ToString());
    }

    private static async Task<ProbeResult> TcpAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        using var client = new TcpClient();
        var sw = Stopwatch.StartNew();
        var task = client.ConnectAsync(host, port);

        var finished = await Task.WhenAny(task, Task.Delay(timeoutMs, ct));
        sw.Stop();

        if (finished != task)
            return new ProbeResult(false, null, $"Timeout TCP {port} ({timeoutMs}ms)");

        // Si ConnectAsync lanzó excepción, la propagamos aquí
        await task;

        return new ProbeResult(true, (int)sw.ElapsedMilliseconds, "");
    }

    private async Task<ProbeResult> HttpAsync(MonitorTarget target, int timeoutMs, CancellationToken ct)
    {
        var url = target.HttpUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            // si no hay URL, construimos algo razonable
            var host = !string.IsNullOrWhiteSpace(target.Fqdn) ? target.Fqdn.Trim() : target.IpAddress.Trim();
            url = $"http://{host}/";
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var client = _httpClientFactory.CreateClient("monitoring");
        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await client.GetAsync(url, cts.Token);
            sw.Stop();
            if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 500)
                return new ProbeResult(true, (int)sw.ElapsedMilliseconds, "");

            return new ProbeResult(false, (int)sw.ElapsedMilliseconds, $"HTTP {(int)resp.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new ProbeResult(false, null, $"Timeout HTTP ({timeoutMs}ms)");
        }
    }
}
