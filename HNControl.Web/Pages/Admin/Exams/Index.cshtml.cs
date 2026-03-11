using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Exams;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(
        Guid Id,
        string Title,
        bool IsActive,
        int Questions,
        int Assigned,
        int Submitted,
        int PendingGrade,
        decimal AvgScore,
        decimal AvgPct
    );

    public List<Row> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        var exams = await _db.Exams
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(200)
            .ToListAsync();

        var qCounts = await _db.ExamQuestions
            .AsNoTracking()
            .GroupBy(q => q.ExamId)
            .Select(g => new { ExamId = g.Key, Cnt = g.Count(), Max = g.Sum(x => x.Points) })
            .ToListAsync();

        var aCounts = await _db.ExamAssignments
            .AsNoTracking()
            .GroupBy(a => a.ExamId)
            .Select(g => new
            {
                ExamId = g.Key,
                Assigned = g.Count(),
                Submitted = g.Count(x => x.Status == ExamAssignmentStatus.Submitted),
                PendingGrade = g.Count(x => x.Status == ExamAssignmentStatus.Submitted)
            })
            .ToListAsync();

        // Avg de graded
        var gradedAvg = await _db.ExamAssignments
            .AsNoTracking()
            .Where(a => a.Status == ExamAssignmentStatus.Graded)
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, AvgScore = g.Average(x => x.Score), AvgMax = g.Average(x => x.MaxScore) })
            .ToListAsync();

        Items = exams.Select(e =>
        {
            var qc = qCounts.FirstOrDefault(x => x.ExamId == e.Id);
            var ac = aCounts.FirstOrDefault(x => x.ExamId == e.Id);
            var ag = gradedAvg.FirstOrDefault(x => x.ExamId == e.Id);

            var avgScore = ag?.AvgScore ?? 0m;
            var avgMax = ag?.AvgMax ?? (qc?.Max ?? 0m);
            var avgPct = avgMax > 0m ? Math.Round((avgScore / avgMax) * 100m, 0) : 0m;

            return new Row(
                e.Id,
                e.Title,
                e.IsActive,
                qc?.Cnt ?? 0,
                ac?.Assigned ?? 0,
                ac?.Submitted ?? 0,
                ac?.PendingGrade ?? 0,
                Math.Round(avgScore, 2),
                avgPct
            );
        }).ToList();
    }

    public async Task<IActionResult> OnGetAssignmentsAsync(Guid id)
    {
        var exam = await _db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (exam == null) return NotFound();

        var rows = await _db.ExamAssignments
            .AsNoTracking()
            .Where(a => a.ExamId == id)
            .Join(
                _db.EmployeeProfiles.AsNoTracking(),
                a => a.UserId,
                e => e.UserId,
                (a, e) => new
                {
                    e.FullName,
                    e.Email,
                    a.Status,
                    a.AssignedAt,
                    a.SubmittedAt,
                    a.Score,
                    a.MaxScore
                })
            .OrderBy(x => x.FullName)
            .Take(250)
            .ToListAsync();

        return new JsonResult(new
        {
            exam = exam.Title,
            total = rows.Count,
            rows = rows.Select(x => new
            {
                x.FullName,
                x.Email,
                Status = x.Status.ToString(),
                AssignedAt = x.AssignedAt,
                SubmittedAt = x.SubmittedAt,
                Score = x.Score,
                MaxScore = x.MaxScore
            })
        });
    }
}
