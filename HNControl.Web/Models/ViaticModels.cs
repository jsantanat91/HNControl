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
    public string UserId { get; set; } = default!;
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByUserId { get; set; }


    // Guardamos la semana por lunes (00:00)
    public DateTime WeekStartDate { get; set; } // date-only logical

    public ViaticWeekStatus Status { get; set; } = ViaticWeekStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public EmployeeProfile? EmployeeProfile { get; set; }
    public List<ViaticEntry> Entries { get; set; } = new();
}

public class ViaticEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WeekId { get; set; }

    public DateTime DayDate { get; set; } // date-only logical
    public ViaticCategory Category { get; set; }

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

    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = "";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ViaticEntry? Entry { get; set; }
}
