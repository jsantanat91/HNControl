using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum MonitorProbeType
{
    IcmpPing = 1,
    TcpConnect = 2,
    HttpGet = 3
}

public enum MonitorStatus
{
    Unknown = 0,
    Up = 1,
    Down = 2
}

/// <summary>
/// Target de monitoreo (ping / tcp / http) ligado a un cliente.
/// </summary>
public class MonitorTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>
    /// Servicio contratado (contrato) opcional.
    /// </summary>
    public Guid? ClientServiceContractId { get; set; }
    public ClientServiceContract? ClientServiceContract { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(255)]
    public string Fqdn { get; set; } = "";

    [MaxLength(64)]
    public string IpAddress { get; set; } = "";

    [MaxLength(32)]
    public string SubnetMask { get; set; } = "";

    [MaxLength(64)]
    public string Gateway { get; set; } = "";

    public MonitorProbeType ProbeType { get; set; } = MonitorProbeType.IcmpPing;

    // Para TCP/HTTP (opcional)
    public int? TcpPort { get; set; }

    [MaxLength(600)]
    public string HttpUrl { get; set; } = "";

    public int CheckIntervalSeconds { get; set; } = 60;
    public int TimeoutMs { get; set; } = 1500;
    public int FailThreshold { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public MonitorStatus LastStatus { get; set; } = MonitorStatus.Unknown;
    public DateTime? LastCheckedAt { get; set; }
    public int? LastLatencyMs { get; set; }

    [MaxLength(500)]
    public string LastError { get; set; } = "";

    public int ConsecutiveFails { get; set; } = 0;

    public DateTime? NextCheckAt { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<MonitorCheck> Checks { get; set; } = new();
}

/// <summary>
/// Historial de checks.
/// </summary>
public class MonitorCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TargetId { get; set; }
    public MonitorTarget? Target { get; set; }

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; } = false;
    public int? LatencyMs { get; set; }

    [MaxLength(500)]
    public string Error { get; set; } = "";
}
