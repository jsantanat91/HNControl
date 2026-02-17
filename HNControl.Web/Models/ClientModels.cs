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

    [MaxLength(400)]
    public string? Address { get; set; }

    // ✅ Columna real en DB
    public ClientType Type { get; set; } = ClientType.Moral;

    // ✅ Lo que tus Pages usan (client.Kind / ClientKind)
    [NotMapped]
    public ClientKind Kind
    {
        get => (ClientKind)Type;
        set => Type = (ClientType)value;
    }

    // Si no la tienes en DB todavía, esto te pedirá migración (y está bien)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ✅ Relación que tu DbContext espera
    public List<ClientService> Services { get; set; } = new();
}
