namespace HNControl.Web.Models;

public enum ClientKind { Fisica = 1, Moral = 2 }

public enum ClientServiceType
{
    Internet = 1,
    Telefonia = 2,
    Servidores = 3,
    Seguridad = 4,
    CCTV = 5,
    Hardware = 6,
    Otro = 99
}

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ClientKind Kind { get; set; }

    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ClientService> Services { get; set; } = new();
}

public class ClientService
{
    public Guid ClientId { get; set; }
    public ClientServiceType ServiceType { get; set; }

    public Client? Client { get; set; }
}
