using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum ServiceOrderType
{
    [Display(Name = "Mantenimiento correctivo")]
    Correctivo = 1,

    [Display(Name = "Mantenimiento preventivo")]
    Preventivo = 2,

    [Display(Name = "Nueva instalacion")]
    NuevaInstalacion = 3,

    [Display(Name = "Levantamiento tecnico")]
    LevantamientoTecnico = 4,

    [Display(Name = "Global (multiple)")]
    Global = 99

    // Para agregar mas opciones:
    // , [Display(Name = "Reubicacion")] Reubicacion = 5
}

public enum ServiceOrderWorkflowArea
{
    [Display(Name = "Levantamiento")]
    Levantamiento = 1,

    [Display(Name = "Materiales")]
    Materiales = 2,

    [Display(Name = "Ejecucion")]
    Ejecucion = 3,

    [Display(Name = "Cierre tecnico")]
    CierreTecnico = 4
}

public enum ServiceOrderStatus
{
    [Display(Name = "Creada")]
    Created = 1,

    [Display(Name = "En proceso")]
    InProgress = 2,

    [Display(Name = "En revision")]
    InReview = 3,

    [Display(Name = "Finalizada")]
    Finalized = 4,

    Completed = 4,

    [Display(Name = "Pendiente firma del cliente")]
    PendingClientSignature = 5,

    [Display(Name = "Rechazada")]
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

    public Guid? ClientServiceContractId { get; set; }
    public ClientServiceContract? ClientServiceContract { get; set; }

    public string? AssignedUserId { get; set; }
    public EmployeeProfile? AssignedEmployee { get; set; }

    public string? ClaimedByUserId { get; set; }
    public EmployeeProfile? ClaimedByEmployee { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public ServiceOrderWorkflowArea CurrentArea { get; set; } = ServiceOrderWorkflowArea.Levantamiento;

    public ServiceOrderType Type { get; set; }
    public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.Created;

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string Description { get; set; } = "";

    [MaxLength(4000)]
    public string LevantamientoNotes { get; set; } = "";

    [MaxLength(4000)]
    public string MaterialesNotes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedForReviewAt { get; set; }

    public DateTime? EstimatedEndDate { get; set; }

    public DateTime? FinalizedAt { get; set; }

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

    public List<ServiceOrderWorkItem> WorkItems { get; set; } = new();

    public List<ServiceOrderChecklistItem> Checklist { get; set; } = new();
    public List<ServiceOrderEvidence> Evidences { get; set; } = new();
    public List<ServiceOrderSignature> Signatures { get; set; } = new();
}

public class ServiceOrderWorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public int SortOrder { get; set; }

    public ServiceOrderType Type { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string Description { get; set; } = "";

    [MaxLength(2000)]
    public string WorkPerformed { get; set; } = "";

    [MaxLength(2000)]
    public string MaterialsUsed { get; set; } = "";

    [MaxLength(2000)]
    public string TechnicianNotes { get; set; } = "";

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ServiceOrderChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public ServiceOrder? Order { get; set; }

    public Guid? WorkItemId { get; set; }
    public ServiceOrderWorkItem? WorkItem { get; set; }

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
    public string StoragePath { get; set; } = "";

    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
}
