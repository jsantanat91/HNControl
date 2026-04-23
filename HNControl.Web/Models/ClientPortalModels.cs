using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum ClientCardDomiciliationStatus
{
    Draft = 1,
    Pending = 2,
    Active = 3,
    Rejected = 4,
    Cancelled = 5
}

public class ClientPortalAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    [MaxLength(40)]
    public string Username { get; set; } = "";

    [MaxLength(512)]
    public string PasswordHash { get; set; } = "";

    [MaxLength(4000)]
    public string PasswordProtected { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    [MaxLength(64)]
    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ClientCardDomiciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    [MaxLength(120)]
    public string MercadoPagoPreferenceId { get; set; } = "";

    [MaxLength(120)]
    public string MercadoPagoExternalReference { get; set; } = "";

    [MaxLength(500)]
    public string InitPointUrl { get; set; } = "";

    public decimal ReferenceAmount { get; set; }
    public ClientCardDomiciliationStatus Status { get; set; } = ClientCardDomiciliationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
