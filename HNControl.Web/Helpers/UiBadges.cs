using HNControl.Web.Models;

namespace HNControl.Web.Helpers;

public static class UiBadges
{
    public static (string text, string css) ExamStatus(ExamAssignmentStatus s) => s switch
    {
        ExamAssignmentStatus.Assigned => ("Asignado", "bg-secondary"),
        ExamAssignmentStatus.InProgress => ("En progreso", "bg-warning text-dark"),
        ExamAssignmentStatus.Submitted => ("En revisión", "bg-info text-dark"),
        ExamAssignmentStatus.Graded => ("Calificado", "bg-success"),
        _ => ("—", "bg-light text-dark")
    };

    // Aprobado/Reprobado por porcentaje (default 80%)
    public static (string text, string css) ExamResult(decimal score, decimal maxScore, decimal passingPct = 80m)
    {
        if (maxScore <= 0) return ("Sin score", "bg-secondary");
        var pct = Math.Round((score / maxScore) * 100m, 1);
        var pass = pct >= passingPct;
        return (pass ? $"Aprobado {pct}%" : $"Reprobado {pct}%", pass ? "bg-success" : "bg-danger");
    }

    public static (string text, string css) LeaveStatus(LeaveRequestStatus s) => s switch
    {
        LeaveRequestStatus.Pending => ("Pendiente", "bg-warning text-dark"),
        LeaveRequestStatus.Approved => ("Aprobado", "bg-success"),
        LeaveRequestStatus.Rejected => ("Rechazado", "bg-danger"),
        LeaveRequestStatus.Cancelled => ("Cancelado", "bg-secondary"),
        _ => ("—", "bg-light text-dark")
    };

    public static (string text, string css) LeaveType(LeaveRequestType t) => t switch
    {
        LeaveRequestType.Vacation => ("Vacaciones", "bg-primary"),
        LeaveRequestType.Medical => ("Médica", "bg-danger"),
        LeaveRequestType.Personal => ("Personal", "bg-dark"),
        _ => ("Otra", "bg-secondary")
    };
}