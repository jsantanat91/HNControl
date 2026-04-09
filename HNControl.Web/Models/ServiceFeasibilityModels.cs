using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum ServiceFeasibilityStatus
{
    Open = 1,
    Accepted = 2,
    Rejected = 3,
    ConvertedToOrder = 4
}

public class ServiceFeasibility
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(400)]
    public string SiteAddress { get; set; } = "";

    [MaxLength(64)]
    public string? Coordinates { get; set; }

    [MaxLength(160)]
    public string SiteContactName { get; set; } = "";

    [MaxLength(60)]
    public string SiteContactPhone { get; set; } = "";

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public ServiceFeasibilityStatus Status { get; set; } = ServiceFeasibilityStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    [MaxLength(64)]
    public string? CreatedByUserId { get; set; }

    public Guid? ConvertedServiceOrderId { get; set; }
    public ServiceOrder? ConvertedServiceOrder { get; set; }
}

