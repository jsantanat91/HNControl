using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum ProjectStatus
{
    Open = 1,
    Active = 1,   // ✅ alias para código viejo que usa "Active"
    Closed = 2
}

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relación cliente
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    // Responsable (tu código viejo lo llama ResponsibleUserId)
    [MaxLength(64)]
    public string AssignedUserId { get; set; } = default!;

    public EmployeeProfile? AssignedEmployee { get; set; }

    // ✅ Alias para páginas/handlers que usan ResponsibleUserId
    [NotMapped]
    public string ResponsibleUserId
    {
        get => AssignedUserId;
        set => AssignedUserId = value ?? "";
    }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    // Fechas / SLA
    public DateTime StartDate { get; set; } = DateTime.Today;

    // En tu UI a veces viene nullable (Input.EstimatedEndDate?)
    public DateTime EstimatedEndDate { get; set; } = DateTime.Today.AddDays(7);

    // Estado
    public ProjectStatus Status { get; set; } = ProjectStatus.Open;

    // Auditoría
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAt { get; set; }

    [MaxLength(64)]
    public string? ClosedByUserId { get; set; }

    // Campos “bonitos” para tu ficha
    [MaxLength(400)]
    public string Objective { get; set; } = "";

    [MaxLength(1200)]
    public string Scope { get; set; } = "";

    // Tu UI usa Description / Comments / AccessNotes
    [MaxLength(4000)]
    public string ActivityDescription { get; set; } = "";

    [MaxLength(2000)]
    public string AdditionalComments { get; set; } = "";

    [MaxLength(8000)]
    public string AccessNotes { get; set; } = "";

    // ✅ Aliases para compatibilidad con páginas que usaban estos nombres:
    [NotMapped]
    public string Description
    {
        get => ActivityDescription;
        set => ActivityDescription = value ?? "";
    }

    [NotMapped]
    public string Comments
    {
        get => AdditionalComments;
        set => AdditionalComments = value ?? "";
    }

    // Accesos estructurados (opcional)
    public List<ProjectAccess> Accesses { get; set; } = new();
}

public class ProjectAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    [MaxLength(120)]
    public string Label { get; set; } = "";

    [MaxLength(300)]
    public string HostOrUrl { get; set; } = "";

    [MaxLength(200)]
    public string Username { get; set; } = "";

    // guardado protegido (DataProtection)
    [MaxLength(2000)]
    public string PasswordProtected { get; set; } = "";

    [MaxLength(800)]
    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
