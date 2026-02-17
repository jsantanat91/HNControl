using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum ServiceOrderType
{
    Correctivo = 1,
    Preventivo = 2,
    NuevaInstalacion = 3
}

public enum ServiceOrderStatus
{
    Created = 1,
    InProgress = 2,
    InReview = 3,
    Finalized = 4,

    // ✅ alias para código viejo que decía "Completed"
    Completed = 4,

    PendingClientSignature = 5,
    Rejected = 6
}

public enum SignatureRole
{
    Technician = 1,
    Client = 2
}

public class ServiceOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public string AssignedUserId { get; set; } = default!;
    public EmployeeProfile? AssignedEmployee { get; set; }

    public ServiceOrderType Type { get; set; }
    public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.Created;

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string Description { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedForReviewAt { get; set; }

    public DateTime? EstimatedEndDate { get; set; }

    public DateTime? FinalizedAt { get; set; }

    // ✅ alias para código viejo que esperaba CompletedAt
    [NotMapped]
    public DateTime? CompletedAt
    {
        get => FinalizedAt;
        set => FinalizedAt = value;
    }

    [MaxLength(64)]
    public string PublicToken { get; set; } = "";

    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? TokenClosedAt { get; set; }

    [MaxLength(500)]
    public string? PdfStoragePath { get; set; }

    public DateTime? PdfGeneratedAt { get; set; }

    public string? AdminReviewNotes { get; set; }

    public List<ServiceOrderChecklistItem> Checklist { get; set; } = new();
    public List<ServiceOrderEvidence> Evidences { get; set; } = new();
    public List<ServiceOrderSignature> Signatures { get; set; } = new();
}

public class ServiceOrderChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(80)]
    public string Category { get; set; } = "General";

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public bool IsRequired { get; set; } = true;

    public bool IsDone { get; set; }
    [MaxLength(600)]
    public string Notes { get; set; } = "";
}

public class ServiceOrderEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(100)]
    public string ContentType { get; set; } = "";

    public long SizeBytes { get; set; }

    [MaxLength(500)]
    public string StoragePath { get; set; } = "";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class ServiceOrderSignature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public SignatureRole Role { get; set; }

    [MaxLength(200)]
    public string SignedByName { get; set; } = "";

    [MaxLength(500)]
    public string StoragePath { get; set; } = ""; // PNG

    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
}
