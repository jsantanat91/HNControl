using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HNControl.Web.Pages.Eval360;

[Authorize]
public class MyResultsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public MyResultsModel(ApplicationDbContext db) => _db = db;

    public Eval360Campaign Campaign { get; set; } = default!;
    public EmployeeProfile Me { get; set; } = default!;

    public record Row(string Competency, decimal AutoPct, decimal OthersPct, decimal DiffPct);
    public List<Row> Rows { get; set; } = new();

    public string LabelsJson { get; set; } = "[]";
    public string AutoJson { get; set; } = "[]";
    public string OthersJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var c = await _db.Eval360Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        Campaign = c;

        var isAdmin = User.IsInRole(AppRoles.Admin);

        if (!isAdmin)
        {
            if (c.Status != Eval360CampaignStatus.Closed) return Forbid();
            if (!c.ResultsVisibleToEmployee) return Forbid();
        }

        Me = await _db.EmployeeProfiles.AsNoTracking().FirstAsync(e => e.UserId == userId);

        await BuildRowsAsync(id, userId);

        if (!Rows.Any())
        {
            TempData["Msg"] = "Aún no hay suficientes respuestas para mostrar resultados.";
        }

        return Page();
    }

    private async Task BuildRowsAsync(Guid campaignId, string subjectUserId)
    {
        var answers = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.Assignment!.CampaignId == campaignId
                        && a.Assignment.SubjectUserId == subjectUserId
                        && a.Assignment.Status == Eval360AssignmentStatus.Submitted)
            .Select(a => new
            {
                a.Assignment!.IsSelf,
                Competency = a.Question!.Competency!.Name,
                Score = a.Score
            })
            .ToListAsync();

        if (!answers.Any()) return;

        var comps = await _db.Eval360Competencies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync();

        foreach (var comp in comps)
        {
            var compAnswers = answers.Where(x => x.Competency == comp).ToList();

            var aSelf = compAnswers.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
            var aOth = compAnswers.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

            decimal autoAvg = aSelf.Any() ? aSelf.Average() : 0m;
            decimal othAvg = aOth.Any() ? aOth.Average() : 0m;

            var autoPct = Math.Round((autoAvg / 5m) * 100m, 0);
            var othPct = Math.Round((othAvg / 5m) * 100m, 0);
            var diffPct = Math.Round(autoPct - othPct, 0);

            Rows.Add(new Row(comp, autoPct, othPct, diffPct));
        }

        LabelsJson = JsonSerializer.Serialize(Rows.Select(r => r.Competency));
        AutoJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.AutoPct));
        OthersJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.OthersPct));
    }
}
