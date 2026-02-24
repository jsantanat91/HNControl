using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Exams;

[Authorize(Roles = AppRoles.Admin)]
public class ResultsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ResultsModel(ApplicationDbContext db) => _db = db;

    public Exam? Exam { get; set; }
    public List<ExamAssignment> Assignments { get; set; } = new();

    public decimal AvgPct { get; set; } = 0m;

    public string StatusLabelsJson { get; set; } = "[]";
    public string StatusValuesJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Exam = await _db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (Exam == null) return NotFound();

        Assignments = await _db.ExamAssignments
            .AsNoTracking()
            .Where(a => a.ExamId == id)
            .Include(a => a.EmployeeProfile)
            .OrderByDescending(a => a.AssignedAt)
            .Take(500)
            .ToListAsync();

        var graded = Assignments.Where(a => a.Status == ExamAssignmentStatus.Graded).ToList();
        if (graded.Count > 0)
        {
            var avgScore = graded.Average(x => x.Score);
            var avgMax = graded.Average(x => x.MaxScore);
            AvgPct = avgMax > 0m ? Math.Round((avgScore / avgMax) * 100m, 0) : 0m;
        }

        var grouped = Assignments
            .GroupBy(a => a.Status)
            .OrderBy(g => g.Key)
            .Select(g => new { Status = g.Key.ToString(), Cnt = g.Count() })
            .ToList();

        StatusLabelsJson = JsonSerializer.Serialize(grouped.Select(x => x.Status));
        StatusValuesJson = JsonSerializer.Serialize(grouped.Select(x => x.Cnt));

        return Page();
    }
}
