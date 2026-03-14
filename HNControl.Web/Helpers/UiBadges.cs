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
        LeaveRequestType.Medical => ("Incidencia medica", "hn-badge-purple"),
        LeaveRequestType.Personal => ("Incidencia personal", "hn-badge-purple"),
        _ => ("Incidencia", "hn-badge-purple")
    };

    public static (string text, string css) ServiceOrderStatusBadge(ServiceOrderStatus s) => s switch
    {
        ServiceOrderStatus.Created => ("Creada", "bg-secondary"),
        ServiceOrderStatus.InProgress => ("En proceso", "bg-primary"),
        ServiceOrderStatus.InReview => ("En revision", "bg-warning text-dark"),
        ServiceOrderStatus.Finalized => ("Finalizada", "bg-success"),
        ServiceOrderStatus.Rejected => ("Rechazada", "bg-danger"),
        ServiceOrderStatus.PendingClientSignature => ("En revision", "bg-warning text-dark"),
        _ => ("-", "bg-light text-dark")
    };

    public static (string text, string css) ServiceOrderTypeBadge(ServiceOrderType t) => t switch
    {
        ServiceOrderType.Correctivo => ("Correctivo", "bg-danger"),
        ServiceOrderType.Preventivo => ("Preventivo", "bg-success"),
        ServiceOrderType.NuevaInstalacion => ("Nueva instalacion", "bg-primary"),
        ServiceOrderType.LevantamientoTecnico => ("Levantamiento", "bg-info text-dark"),
        ServiceOrderType.Eventos => ("Eventos", "bg-info text-dark"),
        ServiceOrderType.Global => ("Global", "bg-dark"),
        _ => ("Otro", "bg-secondary")
    };

    public static (string text, string css) ServiceOrderAreaBadge(ServiceOrderWorkflowArea a) => a switch
    {
        ServiceOrderWorkflowArea.Levantamiento => ("Levantamiento", "bg-info text-dark"),
        ServiceOrderWorkflowArea.Materiales => ("Materiales", "bg-warning text-dark"),
        ServiceOrderWorkflowArea.Ejecucion => ("Ejecucion", "bg-primary"),
        ServiceOrderWorkflowArea.CierreTecnico => ("Cierre tecnico", "bg-success"),
        _ => ("-", "bg-light text-dark")
    };

    public static (string text, string css) ProjectStatusBadge(ProjectStatus s) => s switch
    {
        ProjectStatus.Open => ("Abierto", "bg-primary"),
        ProjectStatus.Closed => ("Cerrado", "bg-secondary"),
        _ => ("-", "bg-light text-dark")
    };

    public static (string text, string css) ProjectSlaBadge(ProjectStatus status, DateTime estimatedEndDate)
    {
        if (status == ProjectStatus.Closed)
            return ("Cerrado", "bg-secondary");

        var days = (estimatedEndDate.Date - DateTime.Today).Days;
        if (days < 0) return ("Vencido", "bg-danger");
        if (days <= 3) return ("Por vencer", "bg-warning text-dark");
        return ("En tiempo", "bg-success");
    }

    public static (string text, string css) KnowledgeStatusBadge(KnowledgeStatus s) => s switch
    {
        KnowledgeStatus.Borrador => ("Borrador", "bg-warning text-dark"),
        KnowledgeStatus.Publicado => ("Publicado", "bg-success"),
        KnowledgeStatus.Archivado => ("Archivado", "bg-secondary"),
        _ => ("-", "bg-light text-dark")
    };

    public static (string text, string css) KnowledgeTypeBadge(KnowledgeDocType t) => t switch
    {
        KnowledgeDocType.ManualInterno => ("Manual", "bg-primary"),
        KnowledgeDocType.AccesoPlataforma => ("Acceso", "bg-danger"),
        KnowledgeDocType.Proceso => ("Proceso", "bg-info text-dark"),
        KnowledgeDocType.Politica => ("Politica", "bg-dark"),
        KnowledgeDocType.Plantilla => ("Plantilla", "bg-warning text-dark"),
        KnowledgeDocType.Referencia => ("Referencia", "bg-secondary"),
        _ => ("Otro", "bg-light text-dark")
    };
}
