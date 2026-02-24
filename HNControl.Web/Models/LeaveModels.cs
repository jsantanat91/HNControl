using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum LeaveRequestType
{
    Vacation = 1,
    Medical = 2,
    Personal = 3,
    Other = 9
}

public enum LeaveRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

/// <summary>
/// Vacaciones e incidencias (médicas, personales, etc.)
/// </summary>
public class LeaveRequest
{
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    public LeaveRequestType Type { get; set; } = LeaveRequestType.Vacation;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int TotalDays { get; set; }

    [MaxLength(1200)]
    public string Reason { get; set; } = "";

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(64)]
    public string? ReviewedByUserId { get; set; }

    [MaxLength(600)]
    public string AdminComment { get; set; } = "";

    /// <summary>
    /// True cuando el admin lo capturó directo (incidencia o vacaciones ya aprobadas).
    /// </summary>
    public bool CreatedByAdmin { get; set; } = false;

    // Navs
    public EmployeeProfile? EmployeeProfile { get; set; }
    public List<LeaveEvidence> Evidences { get; set; } = new();
}

public class LeaveEvidence
{
    public Guid Id { get; set; }

    public Guid LeaveRequestId { get; set; }
    public LeaveRequest? LeaveRequest { get; set; }

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(100)]
    public string ContentType { get; set; } = "";

    [MaxLength(500)]
    public string StoragePath { get; set; } = "";

    public long SizeBytes { get; set; } = 0;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
