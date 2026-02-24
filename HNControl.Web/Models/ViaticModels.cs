using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum ViaticCategory
{
    Transporte = 1,
    Gasolina = 2,
    Material = 3,
    Otros = 4
}

public enum ViaticWeekStatus
{
    [Display(Name = "Borrador")]
    Draft = 1,

    [Display(Name = "Enviado")]
    Submitted = 2,

    [Display(Name = "Aprobado")]
    Approved = 3,

    [Display(Name = "Rechazado")]
    Rejected = 4
}

public class ViaticWeek
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    public DateTime WeekStartDate { get; set; }

    [NotMapped]
    public DateTime WeekStart
    {
        get => WeekStartDate;
        set => WeekStartDate = value.Date;
    }

    public ViaticWeekStatus Status { get; set; } = ViaticWeekStatus.Draft;

    // ✅ Totales “congelables”
    public decimal TotalAmount { get; set; } = 0m;
    public decimal BillableAmount { get; set; } = 0m;

    // ✅ Flujo
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(64)]
    public string? ApprovedByUserId { get; set; }

    // ✅ Nota admin al rechazar
    [MaxLength(1200)]
    public string? AdminNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public EmployeeProfile? EmployeeProfile { get; set; }
    public List<ViaticEntry> Entries { get; set; } = new();
}

public class ViaticEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WeekId { get; set; }

    public DateTime DayDate { get; set; }

    [NotMapped]
    public DateTime WorkDate
    {
        get => DayDate;
        set => DayDate = value.Date;
    }

    public ViaticCategory Category { get; set; }

    [MaxLength(300)]
    public string Description { get; set; } = "";

    public decimal Amount { get; set; }

    public bool IsBillable { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ViaticWeek? Week { get; set; }

    public ViaticAttachment? Attachment { get; set; }
}

public class ViaticAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EntryId { get; set; }

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(100)]
    public string ContentType { get; set; } = "application/pdf";

    public long SizeBytes { get; set; }

    [MaxLength(500)]
    public string StoragePath { get; set; } = "";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ViaticEntry? Entry { get; set; }
}
