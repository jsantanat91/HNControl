namespace HNControl.Web.Models;

public enum ServiceOrderType { Correctivo = 1, Preventivo = 2, NuevaInstalacion = 3 }
public enum ServiceOrderStatus { Created = 1, InProgress = 2, InReview = 3, Finalized = 4 }

public enum SignatureRole { Technician = 1, Client = 2 }

public class ServiceOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public ServiceOrderType Type { get; set; }
    public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.Created;

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public string AssignedUserId { get; set; } = default!;
    public EmployeeProfile? AssignedEmployee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }

    public DateTime? EstimatedEndDate { get; set; } // SLA

    public string PublicToken { get; set; } = Guid.NewGuid().ToString("N");

    public string? PdfStoragePath { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }

    public List<ServiceOrderChecklistItem> Checklist { get; set; } = new();
    public List<ServiceOrderEvidence> Evidences { get; set; } = new();
    public List<ServiceOrderSignature> Signatures { get; set; } = new();
}

public class ServiceOrderChecklistTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ServiceOrderType Type { get; set; }

    public List<ServiceOrderChecklistTemplateItem> Items { get; set; } = new();
}

public class ServiceOrderChecklistTemplateItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public ServiceOrderChecklistTemplate? Template { get; set; }

    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
}

public class ServiceOrderChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
    public string Notes { get; set; } = "";
}

public class ServiceOrderEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class ServiceOrderSignature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public SignatureRole Role { get; set; }
    public string SignedByName { get; set; } = "";
    public string StoragePath { get; set; } = ""; // PNG
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
}
