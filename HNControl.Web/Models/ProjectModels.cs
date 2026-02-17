namespace HNControl.Web.Models;

public enum ProjectStatus { Open = 1, Closed = 2 }

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Title { get; set; } = "";
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime EstimatedEndDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);
    public DateTime? ClosedAt { get; set; }

    public string AssignedUserId { get; set; } = default!;
    public EmployeeProfile? AssignedEmployee { get; set; }

    public string Objective { get; set; } = "";
    public string Scope { get; set; } = "";
    public string ActivityDescription { get; set; } = "";
    public string AdditionalComments { get; set; } = "";

    public ProjectStatus Status { get; set; } = ProjectStatus.Open;

    public List<ProjectAccess> Accesses { get; set; } = new();
}

public class ProjectAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Label { get; set; } = "";        // "DVR", "WiFi", "Cámara 1", etc
    public string HostOrUrl { get; set; } = "";
    public string Username { get; set; } = "";

    // ENCRIPTADO con DataProtection
    public string PasswordProtected { get; set; } = "";
    public string Notes { get; set; } = "";
}
