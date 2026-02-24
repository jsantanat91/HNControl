using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Exams;

[Authorize(Roles = AppRoles.Admin)]
public class GradeModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public GradeModel(ApplicationDbContext db) => _db = db;

    public ExamAssignment? Assignment { get; set; }
    public Exam? Exam { get; set; }

    public record AttachmentVm(Guid Id, string Name, long SizeBytes, DateTime UploadedAt);

    public record OpenVm(Guid AnswerId, Guid QuestionId, ExamQuestionType Type, string QuestionText, decimal Points, string TextAnswer, decimal AutoScore, decimal ManualScore, string Comment, List<AttachmentVm> Attachments);

    public List<OpenVm> OpenAnswers { get; set; } = new();

    [BindProperty]
    public List<GradeInput> Grades { get; set; } = new();

    public class GradeInput
    {
        public Guid AnswerId { get; set; }
        public Guid QuestionId { get; set; }

        [Range(0, 100)]
        public decimal ManualScore { get; set; }

        [MaxLength(1000)]
        public string Comment { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var (a, e) = await LoadAsync(id, tracked: false);
        Assignment = a;
        Exam = e;
        if (Assignment == null || Exam == null) return NotFound();

        BuildOpenList();
        Grades = OpenAnswers.Select(o => new GradeInput
        {
            AnswerId = o.AnswerId,
            QuestionId = o.QuestionId,
            ManualScore = o.ManualScore,
            Comment = o.Comment
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var (a, e) = await LoadAsync(id, tracked: true);
        Assignment = a;
        Exam = e;
        if (Assignment == null || Exam == null) return NotFound();

        // Solo tiene sentido si está Submitted (o si quieres recalificar)
        if (Assignment.Status != ExamAssignmentStatus.Submitted && Assignment.Status != ExamAssignmentStatus.Graded)
        {
            TempData["Error"] = "Este examen aún no está enviado.";
            return RedirectToPage("/Admin/Exams/Results", new { id = Assignment.ExamId });
        }

        // Map preguntas por id
        var qById = Exam.Questions.ToDictionary(q => q.Id);

        foreach (var g in Grades ?? new List<GradeInput>())
        {
            var ans = Assignment.Answers.FirstOrDefault(x => x.Id == g.AnswerId);
            if (ans == null) continue;

            if (!qById.TryGetValue(g.QuestionId, out var q)) continue;

            var max = q.Points;
            var score = g.ManualScore;
            if (score < 0m) score = 0m;
            if (score > max) score = max;

            ans.ManualScore = score;
            ans.Comment = (g.Comment ?? "").Trim();
            ans.UpdatedAt = DateTime.UtcNow;
        }

        Assignment.MaxScore = Exam.Questions.Sum(q => q.Points);
        Assignment.Score = Assignment.Answers.Sum(x => x.AutoScore + x.ManualScore);
        Assignment.Status = ExamAssignmentStatus.Graded;
        Assignment.GradedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Calificación guardada.";
        return RedirectToPage("/Admin/Exams/Results", new { id = Assignment.ExamId });
    }

    private async Task<(ExamAssignment? a, Exam? e)> LoadAsync(Guid assignmentId, bool tracked)
    {
        var q = _db.ExamAssignments
            .Include(a => a.EmployeeProfile)
            .Include(a => a.Exam)
                .ThenInclude(e => e.Questions)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.Attachments)
            .AsQueryable();

        if (!tracked) q = q.AsNoTracking();

        var a = await q.FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (a == null) return (null, null);

        // cargar choices por separado para evitar include monstruoso
        var exam = a.Exam;
        if (exam != null)
        {
            exam.Questions = await _db.ExamQuestions
                .AsNoTracking()
                .Where(x => x.ExamId == exam.Id)
                .OrderBy(x => x.Ordinal)
                .ToListAsync();
        }

        return (a, exam);
    }

    private void BuildOpenList()
    {
        OpenAnswers = new();

        if (Assignment?.Exam == null) return;

        var openQs = Exam!.Questions.Where(q => q.Type == ExamQuestionType.OpenText || q.Type == ExamQuestionType.Attachment).OrderBy(q => q.Ordinal).ToList();

        foreach (var q in openQs)
        {
            var ans = Assignment.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
            if (ans == null)
            {
                // Si el empleado no generó registro (raro), creamos VM vacío
                OpenAnswers.Add(new OpenVm(Guid.Empty, q.Id, q.Type, q.Text, q.Points, "", 0m, 0m, "", new List<AttachmentVm>()));
            }
            else
            {
                OpenAnswers.Add(new OpenVm(ans.Id, q.Id, q.Type, q.Text, q.Points, ans.TextAnswer, ans.AutoScore, ans.ManualScore, ans.Comment, ans.Attachments.OrderByDescending(x => x.UploadedAt).Select(x => new AttachmentVm(x.Id, x.OriginalFileName, x.SizeBytes, x.UploadedAt)).ToList()));
            }
        }
    }
}
