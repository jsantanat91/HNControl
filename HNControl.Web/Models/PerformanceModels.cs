namespace HNControl.Web.Models;

public class PerformanceReview
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public EmployeeProfile? Employee { get; set; }

    public DateTime PeriodStart { get; set; } // quincena
    public DateTime PeriodEnd { get; set; }

    // 1–5
    public int PersonalPerformance { get; set; }
    public int Teamwork { get; set; }
    public int PunctualityAttendance { get; set; }
    public int ProjectExecution { get; set; }
    public int OrderCleanliness { get; set; }
    public int TechnicalSkills { get; set; }

    public string Notes { get; set; } = "";

    public string RatedByUserId { get; set; } = default!;
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;

    public decimal VariablePercent { get; set; } // 0.0–1.0
}
