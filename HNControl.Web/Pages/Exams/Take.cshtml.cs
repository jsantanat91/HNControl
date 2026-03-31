using HNControl.Web.Data;
using HNControl.Web.Models;
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

    public TakeModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    public ExamAssignment? Assignment { get; set; }

    public bool IsReadOnly => Assignment != null &&
                              (Assignment.Status == ExamAssignmentStatus.Submitted ||
                               Assignment.Status == ExamAssignmentStatus.Graded);

    // =========================
    // GET: Render del examen
    // =========================
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
                .ThenInclude(ans => ans.SelectedChoices) // solo para mostrar seleccionadas en UI
            .FirstOrDefaultAsync(a => a.Id == id);

        if (Assignment == null) return NotFound();
        if (!isAdmin && Assignment.UserId != userId) return Forbid();

        // Marcar inicio automático
        if (!IsReadOnly)
        {
            if (Assignment.Status == ExamAssignmentStatus.Assigned)
                Assignment.Status = ExamAssignmentStatus.InProgress;

            Assignment.StartedAt ??= DateTime.UtcNow;

            if (Assignment.MaxScore <= 0m && Assignment.Exam?.Questions != null)
                Assignment.MaxScore = Assignment.Exam.Questions.Sum(q => q.Points);

            await _db.SaveChangesAsync();
        }

        return Page();
    }

    // =========================
    // POST: Guardar (sin enviar)
    // =========================
    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        var (assignment, error) = await LoadForPostAsync(id);
        if (error != null) return error;

        Assignment = assignment;

        if (IsReadOnly)
        {
            TempData["Error"] = "Este examen ya fue enviado.";
            return RedirectToPage(new { id });
        }

        await SaveAnswersFromFormAsync(assignment!, saveOnly: true);

        assignment!.Status = ExamAssignmentStatus.InProgress;
        assignment.StartedAt ??= DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            TempData["Success"] = "Guardado.";
        }
        catch (DbUpdateConcurrencyException)
        {
            // Si el navegador reenvió, o hubo algo raro, no reventamos.
            TempData["Success"] = "Guardado (se evitó un conflicto de concurrencia).";
        }

        return RedirectToPage(new { id });
    }

    // =========================
    // POST: Enviar (submit)
    // =========================
    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        var (assignment, error) = await LoadForPostAsync(id);
        if (error != null) return error;

        Assignment = assignment;

        if (IsReadOnly)
        {
            TempData["Error"] = "Este examen ya fue enviado.";
            return RedirectToPage(new { id });
        }

        // Guardar respuestas y obtener selección por pregunta para autograde (sin depender de navegación trackeada)
        var selectedMap = await SaveAnswersFromFormAsync(assignment!, saveOnly: false);

        // Auto-grade
        var exam = assignment!.Exam!;
        var questions = exam.Questions.OrderBy(q => q.Ordinal).ToList();

        assignment.MaxScore = questions.Sum(q => q.Points);

        foreach (var q in questions)
        {
            var ans = assignment.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
            if (ans == null) continue;

            ans.AutoScore = 0m;

            if (q.Type == ExamQuestionType.OpenText)
            {
                // manual
                ans.AutoScore = 0m;
            }
            else
            {
                var correct = q.Choices.Where(c => c.IsCorrect).Select(c => c.Id).ToHashSet();
                selectedMap.TryGetValue(q.Id, out var selected);
                selected ??= new HashSet<Guid>();

                var ok = correct.SetEquals(selected) && correct.Count > 0;
                ans.AutoScore = ok ? q.Points : 0m;
            }

            ans.UpdatedAt = DateTime.UtcNow;
        }

        assignment.Score = assignment.Answers.Sum(a => a.AutoScore + a.ManualScore);
        assignment.SubmittedAt = DateTime.UtcNow;

        var hasOpen = questions.Any(q => q.Type == ExamQuestionType.OpenText);
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
            // No truena: si fue doble submit / refresh / tab duplicada, ya quedó (o quedó casi).
            TempData["Success"] = "Enviado (se evitó un conflicto de concurrencia).";
            return RedirectToPage(new { id });
        }

        TempData["Success"] = hasOpen
            ? "Enviado. Falta calificación de preguntas abiertas."
            : "Enviado y calificado.";

        return RedirectToPage(new { id });
    }

    // ==========================================================
    // Loader para POST: IMPORTANTÍSIMO -> NO incluir SelectedChoices
    // ==========================================================
    private async Task<(ExamAssignment? assignment, IActionResult? errorResult)> LoadForPostAsync(Guid id)
    {
        var userId = _userMgr.GetUserId(User);
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var assignment = await _db.ExamAssignments
            .AsSplitQuery()
            .Include(a => a.Exam)
                .ThenInclude(e => e.Questions)
                    .ThenInclude(q => q.Choices)
            .Include(a => a.Answers) // <- sin SelectedChoices para evitar tracking de filas viejas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null) return (null, NotFound());
        if (!isAdmin && assignment.UserId != userId) return (null, Forbid());

        return (assignment, null);
    }

    // ==========================================================
    // Guardar respuestas desde Form (anti-concurrencia)
    // - Devuelve selección por QuestionId para autograde
    // ==========================================================
    private async Task<Dictionary<Guid, HashSet<Guid>>> SaveAnswersFromFormAsync(ExamAssignment assignment, bool saveOnly)
    {
        var map = new Dictionary<Guid, HashSet<Guid>>();

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

            // Open text
            if (q.Type == ExamQuestionType.OpenText)
            {
                var tKey = $"t_{q.Id:N}";
                ans.TextAnswer = (Request.Form[tKey].ToString() ?? "").Trim();
            }

            // Choices (Single / Multiple)
            if (q.Type != ExamQuestionType.OpenText)
            {
                var qKey = $"q_{q.Id:N}";
                var selected = Request.Form[qKey].ToArray();

                var selectedIds = selected
                    .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
                    .Where(x => x != Guid.Empty)
                    .ToHashSet();

                map[q.Id] = selectedIds;
                ans.TextAnswer = string.Join(",", selectedIds.Select(x => x.ToString("N")));

                // 🔥 FIX: borrar en BD por filtro (sin EF RemoveRange) para evitar DbUpdateConcurrencyException
                // Nota: NO cargamos SelectedChoices en POST, entonces EF no trackea filas viejas.
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $@"DELETE FROM ""ExamAnswerChoices"" WHERE ""ExamAnswerId"" = {ans.Id}"
                );

                // Insertar nuevas selecciones
                foreach (var cid in selectedIds)
                {
                    _db.ExamAnswerChoices.Add(new ExamAnswerChoice
                    {
                        Id = Guid.NewGuid(),
                        ExamAnswerId = ans.Id,
                        ChoiceId = cid
                    });
                }
            }

            ans.UpdatedAt = DateTime.UtcNow;
        }

        if (assignment.MaxScore <= 0m)
            assignment.MaxScore = questions.Sum(q => q.Points);

        // no requiere nada async adicional, pero dejamos firma async por SQL delete
        await Task.CompletedTask;
        return map;
    }
}
