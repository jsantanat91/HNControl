using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Exams;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public EditModel(ApplicationDbContext db) => _db = db;

    public Exam? Exam { get; set; }

    [BindProperty]
    public ExamInput Input { get; set; } = new();

    public class ExamInput
    {
        public Guid Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [MaxLength(2000)]
        public string Description { get; set; } = "";

        public int? TimeLimitMinutes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Exam = await _db.Exams
            .AsNoTracking()
            .Include(e => e.Questions)
                .ThenInclude(q => q.Choices)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (Exam == null) return NotFound();

        Input = new ExamInput
        {
            Id = Exam.Id,
            Title = Exam.Title,
            Description = Exam.Description,
            TimeLimitMinutes = Exam.TimeLimitMinutes,
            IsActive = Exam.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid) return await ReloadAsync(Input.Id);

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == Input.Id);
        if (exam == null) return NotFound();

        exam.Title = (Input.Title ?? "").Trim();
        exam.Description = (Input.Description ?? "").Trim();
        exam.TimeLimitMinutes = Input.TimeLimitMinutes;
        exam.IsActive = Input.IsActive;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Guardado.";
        return RedirectToPage(new { id = exam.Id });
    }

    public async Task<IActionResult> OnPostAddQuestionAsync(Guid examId, string text, ExamQuestionType type, decimal points, bool isRequired)
    {
        text = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "La pregunta no puede ir vacía.";
            return RedirectToPage(new { id = examId });
        }

        if (points <= 0m) points = 1m;
        if (points > 100m) points = 100m;

        var maxOrd = await _db.ExamQuestions
            .Where(q => q.ExamId == examId)
            .MaxAsync(q => (int?)q.Ordinal) ?? 0;

        var qNew = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            Ordinal = maxOrd + 1,
            Type = type,
            Text = text,
            Points = points,
            IsRequired = isRequired,
            CreatedAt = DateTime.UtcNow
        };

        _db.ExamQuestions.Add(qNew);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Pregunta agregada.";
        return RedirectToPage(new { id = examId });
    }

    public async Task<IActionResult> OnPostDeleteQuestionAsync(Guid examId, Guid questionId)
    {
        var q = await _db.ExamQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ExamId == examId);
        if (q == null) return NotFound();

        _db.ExamQuestions.Remove(q);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Pregunta eliminada.";
        return RedirectToPage(new { id = examId });
    }

    public async Task<IActionResult> OnPostAddChoiceAsync(Guid examId, Guid questionId, string text, bool isCorrect)
    {
        text = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "La opción no puede ir vacía.";
            return RedirectToPage(new { id = examId });
        }

        var q = await _db.ExamQuestions
            .Include(x => x.Choices)
            .FirstOrDefaultAsync(x => x.Id == questionId && x.ExamId == examId);

        if (q == null) return NotFound();

        var maxOrd = q.Choices.Count == 0 ? 0 : (q.Choices.Max(c => (int?)c.Ordinal) ?? 0);

        // SingleChoice: solo una correcta
        if (isCorrect && q.Type == ExamQuestionType.SingleChoice)
        {
            foreach (var c in q.Choices)
                c.IsCorrect = false;
        }

        var cNew = new ExamChoice
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            Ordinal = maxOrd + 1,
            Text = text,
            IsCorrect = isCorrect
        };

        _db.ExamChoices.Add(cNew);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Opción agregada.";
        return RedirectToPage(new { id = examId });
    }

    public async Task<IActionResult> OnPostToggleCorrectAsync(Guid examId, Guid choiceId)
    {
        var choice = await _db.ExamChoices
            .Include(c => c.Question)
            .ThenInclude(q => q!.Choices)
            .FirstOrDefaultAsync(c => c.Id == choiceId);

        if (choice == null || choice.Question == null) return NotFound();

        if (choice.Question.ExamId != examId) return NotFound();

        var newVal = !choice.IsCorrect;

        if (newVal && choice.Question.Type == ExamQuestionType.SingleChoice)
        {
            foreach (var c in choice.Question.Choices)
                c.IsCorrect = false;
        }

        choice.IsCorrect = newVal;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Actualizado.";
        return RedirectToPage(new { id = examId });
    }

    public async Task<IActionResult> OnPostDeleteChoiceAsync(Guid examId, Guid choiceId)
    {
        var c = await _db.ExamChoices
            .Include(x => x.Question)
            .FirstOrDefaultAsync(x => x.Id == choiceId);

        if (c == null || c.Question == null) return NotFound();
        if (c.Question.ExamId != examId) return NotFound();

        _db.ExamChoices.Remove(c);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Opción eliminada.";
        return RedirectToPage(new { id = examId });
    }

    private async Task<IActionResult> ReloadAsync(Guid id)
    {
        // recargar para volver a pintar lista
        return await OnGetAsync(id);
    }
}
