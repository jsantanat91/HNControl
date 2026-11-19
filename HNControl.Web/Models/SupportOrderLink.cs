using System.ComponentModel.DataAnnotations;
namespace HNControl.Web.Models;

// No credentials or customer conversations are stored in this cross-system reference.
public class SupportOrderLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public string SourceId { get; set; } = "hn-support";
    public long TicketId { get; set; }
    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }
    public Guid? InventoryServiceId { get; set; }
    [MaxLength(200)] public string RequestedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
