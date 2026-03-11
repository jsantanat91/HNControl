using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum ClientServiceType
{
    Internet = 1,
    Telefonia = 2,
    CCTV = 3,
    Seguridad = 4,
    Servidores = 5,
    Hardware = 6,
    Otro = 99
}

/// <summary>
/// Servicio contratado por el cliente (contrato individual).
/// Permite múltiples contratos por categoría.
/// Ej: "Internet corporativo" (Telmex, vence 2026-10-31, #contrato 123...).
/// </summary>
public class ClientServiceContract
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>
    /// Ligado opcional a un proyecto (para trazabilidad por sitio/implementación).
    /// </summary>
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public ClientServiceType ServiceType { get; set; } = ClientServiceType.Otro;

    /// <summary>
    /// Nombre corto del contrato.
    /// Ej: "Internet corporativo", "Internet Tultepark", "CCTV Matamoros".
    /// </summary>
    [MaxLength(200)]
    [Column("Name")]
    public string Label { get; set; } = "";

    [MaxLength(120)]
    public string Provider { get; set; } = "";

    [MaxLength(120)]
    public string AccountNumber { get; set; } = "";

    [MaxLength(120)]
    public string ContractNumber { get; set; } = "";

    [MaxLength(300)]
    public string PortalUrl { get; set; } = "";

    [MaxLength(200)]
    public string PortalUsername { get; set; } = "";

    [MaxLength(2000)]
    public string PortalPasswordProtected { get; set; } = "";

    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    /// <summary>
    /// Monto mensual del contrato (costo mensual del servicio durante el contrato).
    /// </summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal? MonthlyAmount { get; set; }

    /// <summary>
    /// Sucursal/sitio del contrato (ej: Matriz, Tultepark, Sucursal Toluca).
    /// </summary>
    [MaxLength(140)]
    public string Branch { get; set; } = "";

    /// <summary>
    /// Direccion del sitio/sucursal para soporte y tickets.
    /// </summary>
    [MaxLength(320)]
    public string BranchAddress { get; set; } = "";

    // Archivo del contrato firmado (PDF recomendado)
    [MaxLength(500)]
    public string? SignedContractStoragePath { get; set; }

    [MaxLength(255)]
    public string? SignedContractOriginalFileName { get; set; }

    [MaxLength(100)]
    public string? SignedContractContentType { get; set; }

    public long? SignedContractSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
