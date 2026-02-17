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
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4
}

public class ViaticWeek
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(64)]
    public string? ApprovedByUserId { get; set; }

    // Guardamos la semana por lunes (00:00)
    public DateTime WeekStartDate { get; set; }

    [NotMapped]
    public DateTime WeekStart
    {
        get => WeekStartDate;
        set => WeekStartDate = value.Date;
    }

    public ViaticWeekStatus Status { get; set; } = ViaticWeekStatus.Draft;

    // ✅ ESTO arregla tu error en Admin/Employees/Details
    public decimal TotalAmount { get; set; } = 0m;

    // ✅ útil para lo facturable/reembolsable
    public decimal BillableAmount { get; set; } = 0m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public EmployeeProfile? EmployeeProfile { get; set; }

    public List<ViaticEntry> Entries { get; set; } = new();
}

public class ViaticEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WeekId { get; set; }

    // date-only lógico
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
