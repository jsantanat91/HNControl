using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Exams;

[Authorize(Policy = "EmployeeOnly")]
public class TakeModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public TakeModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public ExamAssignment? Assignment { get; set; }

    public bool IsReadOnly => Assignment != null &&
                              (Assignment.Status == ExamAssignmentStatus.Submitted ||
                               Assignment.Status == ExamAssignmentStatus.Graded);

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _userMgr.GetUserId(User);
        var isAdmin = User.IsInRole(AppRoles.Admin);

        Assignment = await _db.ExamAssignments
            .AsSplitQuery()
            .Include(a => a.Exam)
                .ThenInclude(e => e.Questions)
                    .ThenInclude(q => q.Choices)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.SelectedChoices)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (Assignment == null) return NotFound();
        if (!isAdmin && Assignment.UserId != userId) return Forbid();

        // marcar inicio automático
        if (!IsReadOnly)
        {
            if (Assignment.Status == ExamAssignmentStatus.Assigned)
                Assignment.Status = ExamAssignmentStatus.InProgress;

            Assignment.StartedAt ??= DateTime.UtcNow;

            // MaxScore inicial
            if (Assignment.MaxScore <= 0m && Assignment.Exam?.Questions != null)
                Assignment.MaxScore = Assignment.Exam.Questions.Sum(q => q.Points);

            await _db.SaveChangesAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        var (assignment, forbidOrNotFound) = await LoadForEditAsync(id);
        if (forbidOrNotFound != null) return forbidOrNotFound;
        Assignment = assignment;

        if (IsReadOnly)
        {
            TempData["Error"] = "Este examen ya fue enviado.";
            return RedirectToPage(new { id });
        }

        await SaveAnswersFromFormAsync(assignment!);

        assignment!.Status = ExamAssignmentStatus.InProgress;
        assignment.StartedAt ??= DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // si el navegador reenvió / doble click: no truena, solo recarga
            TempData["Success"] = "Guardado.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        var (assignment, forbidOrNotFound) = await LoadForEditAsync(id);
        if (forbidOrNotFound != null) return forbidOrNotFound;
        Assignment = assignment;

        if (IsReadOnly)
        {
            TempData["Error"] = "Este examen ya fue enviado.";
            return RedirectToPage(new { id });
        }

        await SaveAnswersFromFormAsync(assignment!);

        // Auto-grade
        var exam = assignment!.Exam!;
        var questions = exam.Questions.OrderBy(q => q.Ordinal).ToList();
        assignment.MaxScore = questions.Sum(q => q.Points);

        foreach (var q in questions)
        {
            var ans = assignment.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
            if (ans == null) continue;

            ans.AutoScore = 0m;

            if (q.Type == ExamQuestionType.OpenText || q.Type == ExamQuestionType.Attachment)
            {
                ans.AutoScore = 0m; // manual
            }
            else
            {
                var correct = q.Choices.Where(c => c.IsCorrect).Select(c => c.Id).ToHashSet();
                var selected = ans.SelectedChoices.Select(sc => sc.ChoiceId).ToHashSet();

                // Exact match
                var ok = correct.SetEquals(selected) && correct.Count > 0;
                ans.AutoScore = ok ? q.Points : 0m;
            }

            ans.UpdatedAt = DateTime.UtcNow;
        }

        assignment.Score = assignment.Answers.Sum(a => a.AutoScore + a.ManualScore);
        assignment.SubmittedAt = DateTime.UtcNow;

        var hasOpen = questions.Any(q => q.Type == ExamQuestionType.OpenText || q.Type == ExamQuestionType.Attachment);
        if (hasOpen)
        {
            assignment.Status = ExamAssignmentStatus.Submitted;
        }
        else
        {
            assignment.Status = ExamAssignmentStatus.Graded;
            assignment.GradedAt = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // si ya se envió en otro POST (doble click / refresh / dos tabs), no explota
            var latest = await _db.ExamAssignments.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (latest != null && latest.Status >= ExamAssignmentStatus.Submitted)
            {
                TempData["Success"] = "Enviado (ya estaba enviado, solo se evitó el doble envío).";
                return RedirectToPage(new { id });
            }

            throw; // si fue otra cosa real, que reviente para verla
        }

        TempData["Success"] = hasOpen
            ? "Enviado. Falta calificación de preguntas abiertas."
            : "Enviado y calificado.";

        return RedirectToPage(new { id });
    }

    private async Task<(ExamAssignment? assignment, IActionResult? errorResult)> LoadForEditAsync(Guid id)
    {
        var userId = _userMgr.GetUserId(User);
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var assignment = await _db.ExamAssignments
            .AsSplitQuery()
            .Include(a => a.Exam)
                .ThenInclude(e => e.Questions)
                    .ThenInclude(q => q.Choices)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.SelectedChoices)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null) return (null, NotFound());
        if (!isAdmin && assignment.UserId != userId) return (null, Forbid());

        return (assignment, null);
    }

    private async Task SaveAnswersFromFormAsync(ExamAssignment assignment)
    {
        var exam = assignment.Exam!;
        var questions = exam.Questions.OrderBy(q => q.Ordinal).ToList();

        foreach (var q in questions)
        {
            var ans = assignment.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
            if (ans == null)
            {
                ans = new ExamAnswer
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = assignment.Id,
                    QuestionId = q.Id,
                    TextAnswer = "",
                    AutoScore = 0m,
                    ManualScore = 0m,
                    Comment = "",
                    UpdatedAt = DateTime.UtcNow
                };
                _db.ExamAnswers.Add(ans);
                assignment.Answers.Add(ans);
            }

            // Open text / Attachment text
            if (q.Type == ExamQuestionType.OpenText || q.Type == ExamQuestionType.Attachment)
            {
                var tKey = $"t_{q.Id:N}";
                ans.TextAnswer = (Request.Form[tKey].ToString() ?? "").Trim();
            }

            // Attachments (diagrama / evidencia)
            if (q.Type == ExamQuestionType.Attachment)
            {
                var fKey = $"f_{q.Id:N}";
                var files = Request.Form.Files.GetFiles(fKey);

                if (files != null && files.Count > 0)
                {
                    var subFolder = $"exams/{assignment.Id:N}";
                    var allowedExt = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".heic" };

                    foreach (var file in files)
                    {
                        var attId = Guid.NewGuid();
                        var (storagePath, sizeBytes, contentType, originalName) = await _storage.SaveFileAsync(
                            file,
                            subFolder,
                            attId.ToString("N"),
                            allowedExt,
                            15 * 1024 * 1024
                        );

                        var att = new ExamAnswerAttachment
                        {
                            Id = attId,
                            ExamAnswerId = ans.Id,
                            OriginalFileName = originalName,
                            ContentType = contentType,
                            StoragePath = storagePath,
                            SizeBytes = sizeBytes,
                            UploadedAt = DateTime.UtcNow
                        };

                        _db.ExamAnswerAttachments.Add(att);
                        ans.Attachments.Add(att);
                    }
                }
            }

            // Choices
            var qKey = $"q_{q.Id:N}";
            var selected = Request.Form[qKey].ToArray();
            var selectedIds = selected
                .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToHashSet();

            // ✅ FIX: borrar selecciones viejas por filtro (idempotente, no revienta si ya se borraron)
            await _db.ExamAnswerChoices
                .Where(x => x.ExamAnswerId == ans.Id)
                .ExecuteDeleteAsync();

            ans.SelectedChoices.Clear();

            foreach (var cid in selectedIds)
            {
                var eac = new ExamAnswerChoice
                {
                    Id = Guid.NewGuid(),
                    ExamAnswerId = ans.Id,
                    ChoiceId = cid
                };

                _db.ExamAnswerChoices.Add(eac);
                ans.SelectedChoices.Add(eac);
            }

            ans.UpdatedAt = DateTime.UtcNow;
        }

        if (assignment.MaxScore <= 0m)
            assignment.MaxScore = questions.Sum(q => q.Points);
    }
}