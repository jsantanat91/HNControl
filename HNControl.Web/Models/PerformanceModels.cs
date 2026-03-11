using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public class PerformanceReview
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    // Navegación opcional (para Include)
    [ForeignKey(nameof(UserId))]
    public EmployeeProfile? Employee { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // 1–5
    [Range(1, 5)] public int PersonalPerformance { get; set; } = 3;
    [Range(1, 5)] public int Teamwork { get; set; } = 3;
    [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
    [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
    [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
    [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

    // 0..1
    public decimal VariablePercent { get; set; } = 0m;

    [MaxLength(3600)]
    public string Notes { get; set; } = "";

    [MaxLength(64)]
    public string? RatedByUserId { get; set; }

    public DateTime? RatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void Recalc()
    {
        var avg = (PersonalPerformance + Teamwork + PunctualityAttendance +
                   ProjectExecution + OrderCleanliness + TechnicalSkills) / 6m;

        VariablePercent = Math.Round(avg / 5m, 4); // 0..1
        UpdatedAt = DateTime.UtcNow;
    }
}
