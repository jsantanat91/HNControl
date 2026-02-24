using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum ExamQuestionType
{
    SingleChoice = 1,
    MultipleChoice = 2,
    OpenText = 3,
    Attachment = 4
}

public enum ExamAssignmentStatus
{
    Assigned = 0,
    InProgress = 1,
    Submitted = 2,
    Graded = 3
}

public class Exam
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string Description { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int? TimeLimitMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? CreatedByUserId { get; set; }

    public List<ExamQuestion> Questions { get; set; } = new();
    public List<ExamAssignment> Assignments { get; set; } = new();
}

public class ExamQuestion
{
    public Guid Id { get; set; }

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public int Ordinal { get; set; } = 1;

    public ExamQuestionType Type { get; set; } = ExamQuestionType.SingleChoice;

    [MaxLength(2000)]
    public string Text { get; set; } = "";

    public decimal Points { get; set; } = 1m;

    public bool IsRequired { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ExamChoice> Choices { get; set; } = new();
}

public class ExamChoice
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }
    public ExamQuestion? Question { get; set; }

    public int Ordinal { get; set; } = 1;

    [MaxLength(1000)]
    public string Text { get; set; } = "";

    public bool IsCorrect { get; set; } = false;
}

public class ExamAssignment
{
    public Guid Id { get; set; }

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    public EmployeeProfile? EmployeeProfile { get; set; }

    public ExamAssignmentStatus Status { get; set; } = ExamAssignmentStatus.Assigned;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DueAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? GradedAt { get; set; }

    public decimal Score { get; set; } = 0m;

    public decimal MaxScore { get; set; } = 0m;

    public List<ExamAnswer> Answers { get; set; } = new();
}

public class ExamAnswer
{
    public Guid Id { get; set; }

    public Guid AssignmentId { get; set; }
    public ExamAssignment? Assignment { get; set; }

    public Guid QuestionId { get; set; }
    public ExamQuestion? Question { get; set; }

    public string TextAnswer { get; set; } = "";

    public decimal AutoScore { get; set; } = 0m;

    public decimal ManualScore { get; set; } = 0m;

    [MaxLength(1000)]
    public string Comment { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ExamAnswerChoice> SelectedChoices { get; set; } = new();

    public List<ExamAnswerAttachment> Attachments { get; set; } = new();
}

public class ExamAnswerAttachment
{
    public Guid Id { get; set; }

    public Guid ExamAnswerId { get; set; }
    public ExamAnswer? Answer { get; set; }

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(100)]
    public string ContentType { get; set; } = "";

    [MaxLength(500)]
    public string StoragePath { get; set; } = "";

    public long SizeBytes { get; set; } = 0;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}


public class ExamAnswerChoice
{
    public Guid Id { get; set; }

    public Guid ExamAnswerId { get; set; }
    public ExamAnswer? Answer { get; set; }

    public Guid ChoiceId { get; set; }
    public ExamChoice? Choice { get; set; }
}
