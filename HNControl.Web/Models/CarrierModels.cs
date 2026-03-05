using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

/// <summary>
/// Proveedor / Carrier (Telmex, Totalplay, Axtel, etc.)
/// </summary>
public class InternetCarrier
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    /// <summary>
    /// Nombre del ejecutivo / contacto principal del carrier.
    /// </summary>
    [MaxLength(120)]
    public string ExecutiveName { get; set; } = "";

    // Logo (opcional)
    [MaxLength(500)]
    public string? LogoStoragePath { get; set; }

    [MaxLength(255)]
    public string? LogoOriginalFileName { get; set; }

    [MaxLength(100)]
    public string? LogoContentType { get; set; }

    public long? LogoSizeBytes { get; set; }

    [MaxLength(40)]
    public string SupportPhone { get; set; } = "";

    [MaxLength(120)]
    public string SupportEmail { get; set; } = "";

    [MaxLength(400)]
    public string SupportPortalUrl { get; set; } = "";

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ClientCarrierService> Services { get; set; } = new();
}

/// <summary>
/// Servicio del carrier ligado a un cliente (ej: Internet dedicado 200Mbps)
/// </summary>
public class ClientCarrierService
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid CarrierId { get; set; }
    public InternetCarrier? Carrier { get; set; }

    public Guid? ClientServiceContractId { get; set; }
    public ClientServiceContract? ClientServiceContract { get; set; }

    [Required, MaxLength(140)]
    public string ServiceLabel { get; set; } = ""; // Nombre interno: "Matriz - Dedicado"

    [MaxLength(140)]
    public string Plan { get; set; } = "";

    [MaxLength(120)]
    public string AccountNumber { get; set; } = "";

    [MaxLength(120)]
    public string ContractNumber { get; set; } = "";

    [MaxLength(120)]
    public string CircuitId { get; set; } = "";

    [MaxLength(200)]
    public string ServiceAddress { get; set; } = "";

    [MaxLength(200)]
    public string IpInfo { get; set; } = ""; // IP pública, rango, gateway (texto corto)

    [MaxLength(40)]
    public string SupportPhoneOverride { get; set; } = ""; // si este servicio tiene teléfono distinto

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ClientCarrierNote> CarrierNotes { get; set; } = new();
}

public enum CarrierNoteType
{
    Info = 1,
    Ticket = 2,
    Incident = 3
}

/// <summary>
/// Bitácora interna: contacto con carrier, ticket, falla, etc.
/// (Empleado: puede agregar; Admin: puede ver + crear/editar servicios)
/// </summary>
public class ClientCarrierNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServiceId { get; set; }
    public ClientCarrierService? Service { get; set; }

    public CarrierNoteType NoteType { get; set; } = CarrierNoteType.Info;

    [MaxLength(120)]
    public string TicketNumber { get; set; } = "";

    [Required, MaxLength(3000)]
    public string Message { get; set; } = "";

    [MaxLength(64)]
    public string CreatedByUserId { get; set; } = "";

    [MaxLength(200)]
    public string CreatedByName { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
