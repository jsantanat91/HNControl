using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public enum Eval360CampaignStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2
}

public enum Eval360AssignmentStatus
{
    Pending = 0,
    Submitted = 1
}

public class Eval360Competency
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public List<Eval360Question> Questions { get; set; } = new();
}

public class Eval360Question
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompetencyId { get; set; }

    [ForeignKey(nameof(CompetencyId))]
    public Eval360Competency? Competency { get; set; }

    [MaxLength(600)]
    public string Text { get; set; } = "";

    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class Eval360Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(800)]
    public string Description { get; set; } = "";

    // Opcional (solo informativo)
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }

    public Eval360CampaignStatus Status { get; set; } = Eval360CampaignStatus.Draft;

    // Incluye autoevaluación (asignación evaluator=subject)
    public bool AllowSelf { get; set; } = true;

    // Si false, el empleado no ve resultados (solo admin)
    public bool ResultsVisibleToEmployee { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Eval360Assignment> Assignments { get; set; } = new();
}

public class Eval360Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Eval360Campaign? Campaign { get; set; }

    [MaxLength(64)]
    public string EvaluatorUserId { get; set; } = "";

    [MaxLength(64)]
    public string SubjectUserId { get; set; } = "";

    public bool IsSelf { get; set; } = false;

    public Eval360AssignmentStatus Status { get; set; } = Eval360AssignmentStatus.Pending;

    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Eval360Answer> Answers { get; set; } = new();
    public List<Eval360Comment> Comments { get; set; } = new();
}

public class Eval360Answer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssignmentId { get; set; }

    [ForeignKey(nameof(AssignmentId))]
    public Eval360Assignment? Assignment { get; set; }

    public Guid QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public Eval360Question? Question { get; set; }

    // 1..5
    [Range(1, 5)]
    public int Score { get; set; } = 3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Eval360Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssignmentId { get; set; }

    [ForeignKey(nameof(AssignmentId))]
    public Eval360Assignment? Assignment { get; set; }

    public Guid CompetencyId { get; set; }

    [ForeignKey(nameof(CompetencyId))]
    public Eval360Competency? Competency { get; set; }

    [MaxLength(2000)]
    public string CommentText { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
