using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum ClientLegalDocumentType
{
    Contract = 1,
    NDA = 2
}

public enum ClientLegalDocumentStatus
{
    Draft = 1,
    SentForSignature = 2,
    Signed = 3
}

public class ClientLegalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ClientServiceContractId { get; set; }
    public ClientServiceContract? ClientServiceContract { get; set; }

    public ClientLegalDocumentType DocumentType { get; set; } = ClientLegalDocumentType.Contract;
    public ClientLegalDocumentStatus Status { get; set; } = ClientLegalDocumentStatus.Draft;

    [MaxLength(220)]
    public string Title { get; set; } = "";

    [MaxLength(8000)]
    public string TermsBody { get; set; } = "";

    public decimal? MonthlyAmount { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    [MaxLength(80)]
    public string PublicToken { get; set; } = "";
    public DateTime? TokenExpiresAt { get; set; }

    [MaxLength(200)]
    public string? SignedByName { get; set; }

    [MaxLength(256)]
    public string? SignedByEmail { get; set; }

    [MaxLength(500)]
    public string? SignatureStoragePath { get; set; }
    public DateTime? SignedAt { get; set; }

    [MaxLength(500)]
    public string? PdfStoragePath { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ProjectDeliveryFormatStatus
{
    Draft = 1,
    SentForSignature = 2,
    Signed = 3
}

public class ProjectDeliveryFormat
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    [MaxLength(220)]
    public string Title { get; set; } = "";

    [MaxLength(4000)]
    public string ServiceSummary { get; set; } = "";

    [MaxLength(4000)]
    public string EquipmentSummary { get; set; } = "";

    [MaxLength(320)]
    public string DeliveryLocation { get; set; } = "";

    [MaxLength(200)]
    public string ReceiverName { get; set; } = "";

    [MaxLength(256)]
    public string ReceiverEmail { get; set; } = "";

    [MaxLength(40)]
    public string ReceiverPhone { get; set; } = "";

    public DateTime DeliveryDate { get; set; } = DateTime.Today;
    public ProjectDeliveryFormatStatus Status { get; set; } = ProjectDeliveryFormatStatus.Draft;

    [MaxLength(80)]
    public string PublicToken { get; set; } = "";
    public DateTime? TokenExpiresAt { get; set; }

    [MaxLength(200)]
    public string? SignedByName { get; set; }

    [MaxLength(256)]
    public string? SignedByEmail { get; set; }

    [MaxLength(500)]
    public string? SignatureStoragePath { get; set; }
    public DateTime? SignedAt { get; set; }

    [MaxLength(500)]
    public string? PdfStoragePath { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
