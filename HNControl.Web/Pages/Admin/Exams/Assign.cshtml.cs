using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Exams;

[Authorize(Roles = AppRoles.Admin)]
public class AssignModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public AssignModel(ApplicationDbContext db) => _db = db;

    public Exam? Exam { get; set; }

    public List<EmployeeProfile> Employees { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid ExamId { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? DueAt { get; set; }

        public List<string> SelectedUserIds { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Exam = await _db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (Exam == null) return NotFound();

        Employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        Input.ExamId = id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Exam = await _db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Input.ExamId);
        if (Exam == null) return NotFound();

        Employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        if (Input.SelectedUserIds == null || Input.SelectedUserIds.Count == 0)
        {
            ModelState.AddModelError("", "Selecciona al menos un empleado.");
            return Page();
        }

        var maxScore = await _db.ExamQuestions
            .AsNoTracking()
            .Where(q => q.ExamId == Input.ExamId)
            .SumAsync(q => (decimal?)q.Points) ?? 0m;

        var dueUtc = Input.DueAt.HasValue ? DateTime.SpecifyKind(Input.DueAt.Value, DateTimeKind.Utc) : (DateTime?)null;

        // Cargar asignaciones existentes (activas)
        var activeStatuses = new[] { ExamAssignmentStatus.Assigned, ExamAssignmentStatus.InProgress, ExamAssignmentStatus.Submitted };

        var existing = await _db.ExamAssignments
            .AsNoTracking()
            .Where(a => a.ExamId == Input.ExamId && activeStatuses.Contains(a.Status))
            .Select(a => a.UserId)
            .ToListAsync();

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        foreach (var uid in Input.SelectedUserIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(uid)) continue;
            if (existingSet.Contains(uid)) continue;

            _db.ExamAssignments.Add(new ExamAssignment
            {
                Id = Guid.NewGuid(),
                ExamId = Input.ExamId,
                UserId = uid,
                Status = ExamAssignmentStatus.Assigned,
                AssignedAt = DateTime.UtcNow,
                DueAt = dueUtc,
                Score = 0m,
                MaxScore = maxScore
            });
            created++;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Asignado a {created} empleado(s).";
        return RedirectToPage("/Admin/Exams/Results", new { id = Input.ExamId });
    }
}
