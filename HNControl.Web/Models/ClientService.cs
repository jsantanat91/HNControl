using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

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

public class ClientService
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public ClientServiceType ServiceType { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }
}
