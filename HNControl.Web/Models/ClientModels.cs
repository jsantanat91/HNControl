using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum ClientType
{
    Moral = 1,
    Fisica = 2
}

// ✅ Alias para Pages viejas que usan ClientKind
public enum ClientKind
{
    Moral = 1,
    Fisica = 2
}

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(20)]
    public string ClientCode { get; set; } = "";

    [Required, MaxLength(200)]
    [Display(Name = "Razón Social / Nombre")]
    public string Name { get; set; } = "";

    [MaxLength(13)]
    [Display(Name = "RFC")]
    public string? Rfc { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(120)]
    [Display(Name = "Nombre de contacto")]
    public string? ContactName { get; set; }

    [MaxLength(400)]
    public string? Address { get; set; }

    [MaxLength(160)]
    [Display(Name = "Representante legal")]
    public string? LegalRepresentative { get; set; }

    [MaxLength(256)]
    [Display(Name = "Correo legal")]
    public string? LegalEmail { get; set; }

    [MaxLength(120)]
    [Display(Name = "Puesto representante")]
    public string? LegalPosition { get; set; }

    [MaxLength(180)]
    [Display(Name = "Giro / actividad")]
    public string? BusinessLine { get; set; }

    [MaxLength(256)]
    [Display(Name = "Correo de facturacion")]
    public string? BillingEmail { get; set; }

    [MaxLength(400)]
    [Display(Name = "Domicilio fiscal")]
    public string? FiscalAddress { get; set; }

    [MaxLength(10)]
    [Display(Name = "Codigo postal fiscal")]
    public string? FiscalZipCode { get; set; }

    [MaxLength(4)]
    [Display(Name = "Regimen fiscal SAT")]
    public string? FiscalRegimeCode { get; set; }

    [MaxLength(4)]
    [Display(Name = "Uso CFDI por defecto")]
    public string? CfdiUseCodeDefault { get; set; }

    [MaxLength(80)]
    public string? PublicQuoteToken { get; set; }

    // ✅ Columna real en DB
    public ClientType Type { get; set; } = ClientType.Moral;

    // ✅ Lo que tus Pages usan (client.Kind / ClientKind)
    [NotMapped]
    public ClientKind Kind
    {
        get => (ClientKind)Type;
        set => Type = (ClientType)value;
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsTemporaryLead { get; set; }
    [MaxLength(64)]
    public string? CreatedByUserId { get; set; }
    public DateTime? ConvertedToFormalAt { get; set; }

    // ✅ Contratos/servicios del cliente (múltiples por categoría)
    public List<ClientServiceContract> Contracts { get; set; } = new();
    public List<ClientContact> Contacts { get; set; } = new();
    public List<ClientLegalDocument> LegalDocuments { get; set; } = new();

    // ✅ Alias por compatibilidad (si algún Page viejo aún usa Client.Services)
    [NotMapped]
    public List<ClientServiceContract> Services
    {
        get => Contracts;
        set => Contracts = value ?? new();
    }
}

public class ClientContact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    [Required, MaxLength(180)]
    public string Name { get; set; } = "";

    [MaxLength(256)]
    public string Email { get; set; } = "";

    [MaxLength(60)]
    public string Phone { get; set; } = "";

    [MaxLength(120)]
    public string Role { get; set; } = "";

    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
