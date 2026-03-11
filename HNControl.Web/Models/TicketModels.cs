using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum TicketSource
{
    PublicPortal = 1,
    MonitoringAuto = 2,
    InternalManual = 3
}

public enum TicketStatus
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    PendingCustomer = 4,
    Resolved = 5,
    Closed = 6,
    Cancelled = 7
}

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum TicketImpact
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum TicketUrgency
{
    Low = 1,
    Medium = 2,
    High = 3
}

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(40)]
    public string TicketNumber { get; set; } = "";

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ClientServiceContractId { get; set; }
    public ClientServiceContract? ClientServiceContract { get; set; }

    public Guid? MonitorTargetId { get; set; }
    public MonitorTarget? MonitorTarget { get; set; }

    [Required, MaxLength(220)]
    public string Title { get; set; } = "";

    [MaxLength(4000)]
    public string Description { get; set; } = "";

    [MaxLength(100)]
    public string Category { get; set; } = "Incidente";

    [MaxLength(100)]
    public string Subcategory { get; set; } = "";

    public TicketSource Source { get; set; } = TicketSource.InternalManual;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketImpact Impact { get; set; } = TicketImpact.Medium;
    public TicketUrgency Urgency { get; set; } = TicketUrgency.Medium;

    [MaxLength(64)]
    public string? AssignedToUserId { get; set; }
    public EmployeeProfile? AssignedToEmployee { get; set; }

    [MaxLength(200)]
    public string AssignedToName { get; set; } = "";

    [MaxLength(64)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(200)]
    public string CreatedByName { get; set; } = "";

    [MaxLength(180)]
    public string RequesterName { get; set; } = "";

    [MaxLength(256)]
    public string RequesterEmail { get; set; } = "";

    [MaxLength(60)]
    public string RequesterPhone { get; set; } = "";

    [MaxLength(300)]
    public string RequesterLocation { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime SlaResponseDueAt { get; set; }
    public DateTime SlaResolutionDueAt { get; set; }
    public bool SlaBreachedResponse { get; set; }
    public bool SlaBreachedResolution { get; set; }

    [MaxLength(1200)]
    public string ResolutionSummary { get; set; } = "";

    public List<TicketEvent> Events { get; set; } = new();
}

public class TicketEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [MaxLength(60)]
    public string EventType { get; set; } = "Note";

    [MaxLength(64)]
    public string UserId { get; set; } = "";

    [MaxLength(200)]
    public string UserName { get; set; } = "";

    [MaxLength(4000)]
    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

