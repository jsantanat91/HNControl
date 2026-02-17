using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum ServiceOrderAuditEvent
{
    ChecklistSaved,
    EvidenceUploaded,
    TechnicianSigned,
    ClientSigned,
    SubmitAttempt,
    SubmittedForReview,
    StatusChanged,
    PdfGenerated,
    EmailSent,
    Rejected,
    Completed,
    TokenExpired,
    TokenClosed
}

public class ServiceOrderAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public ServiceOrderAuditEvent EventType { get; set; }

    [MaxLength(40)]
    public string ActorType { get; set; } = "public"; // public/admin/employee/system

    [MaxLength(120)]
    public string? ActorId { get; set; }

    [MaxLength(200)]
    public string? ActorName { get; set; }

    [MaxLength(80)]
    public string? IpAddress { get; set; }

    [MaxLength(800)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
