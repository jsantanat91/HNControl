using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HNControl.Web.Pages.Admin.Eval360.Results;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public Eval360Campaign Campaign { get; set; } = default!;
    public List<EmployeeProfile> Employees { get; set; } = new();

    public List<SelectListItem> CampaignItems { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    public record Row(string Competency, decimal AutoPct, decimal OthersPct, decimal DiffPct, string LevelAuto, string LevelOthers);

    public List<Row> Rows { get; set; } = new();

    public string LabelsJson { get; set; } = "[]";
    public string AutoJson { get; set; } = "[]";
    public string OthersJson { get; set; } = "[]";

    // Cumulativo del equipo
    public int TeamEmployeesCount { get; set; } = 0;
    public string TeamLabelsJson { get; set; } = "[]";
    public string TeamExpectedJson { get; set; } = "[]";
    public string TeamEvalJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var campaigns = await _db.Eval360Campaigns
            .AsNoTracking()
            .OrderByDescending(x => x.PeriodStart ?? x.CreatedAt)
            .ToListAsync();

        CampaignItems = campaigns
            .Select(x =>
            {
                var ym = (x.PeriodStart ?? x.CreatedAt).ToString("yyyy-MM");
                var txt = $"{ym} · {x.Title}";
                return new SelectListItem(txt, x.Id.ToString(), x.Id == id);
            })
            .ToList();

        var c = campaigns.FirstOrDefault(x => x.Id == id);
        if (c == null) return RedirectToPage("/Admin/Eval360/Campaigns/Index");
        Campaign = c;

        Employees = await _db.EmployeeProfiles.AsNoTracking()
            .Where(e => !e.Email.ToLower().EndsWith("@hn.local"))
            .OrderBy(e => e.FullName)
            .ToListAsync();

        if (!Employees.Any())
        {
            TempData["Msg"] = "No hay empleados para mostrar.";
            return Page();
        }

        UserId ??= Employees.First().UserId;

        await BuildRowsAsync(id, UserId);
        await BuildTeamAsync(id);

        return Page();
    }

    private static string Level(decimal pct)
    {
        if (pct <= 0) return "Sin datos";
        if (pct < 50) return "Crítico";
        if (pct < 75) return "Alto";
        if (pct < 85) return "Moderado";
        return "Desarrollado";
    }

    private async Task BuildRowsAsync(Guid campaignId, string subjectUserId)
    {
        Rows.Clear();

        // Promedios por competencia para Esperado (self) vs Evaluación (otros)
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

        var comps = await _db.Eval360Competencies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync();

        foreach (var comp in comps)
        {
            var compAnswers = answers.Where(x => x.Competency == comp).ToList();

            decimal expectedAvg = 0m;
            decimal evalAvg = 0m;

            var aSelf = compAnswers.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
            var aOth = compAnswers.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

            if (aSelf.Any()) expectedAvg = aSelf.Average();
            if (aOth.Any()) evalAvg = aOth.Average();

            var expectedPct = Math.Round((expectedAvg / 5m) * 100m, 0);
            var evalPct = Math.Round((evalAvg / 5m) * 100m, 0);
            var diffPct = Math.Round(expectedPct - evalPct, 0);

            Rows.Add(new Row(comp, expectedPct, evalPct, diffPct, Level(expectedPct), Level(evalPct)));
        }

        LabelsJson = JsonSerializer.Serialize(Rows.Select(r => r.Competency));
        AutoJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.AutoPct));
        OthersJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.OthersPct));
    }

    private async Task BuildTeamAsync(Guid campaignId)
    {
        // Trae todo lo necesario en una sola vuelta y calcula promedio por empleado (para no sesgar por # evaluadores)
        var raw = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.Assignment!.CampaignId == campaignId
                        && a.Assignment.Status == Eval360AssignmentStatus.Submitted)
            .Select(a => new
            {
                a.Assignment!.SubjectUserId,
                a.Assignment!.IsSelf,
                Competency = a.Question!.Competency!.Name,
                Score = a.Score
            })
            .ToListAsync();

        if (!raw.Any())
        {
            TeamEmployeesCount = 0;
            TeamLabelsJson = TeamExpectedJson = TeamEvalJson = "[]";
            return;
        }

        var comps = await _db.Eval360Competencies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync();

        var subjects = raw.Select(x => x.SubjectUserId).Distinct().ToList();
        TeamEmployeesCount = subjects.Count;

        var teamLabels = new List<string>();
        var teamExpected = new List<double>();
        var teamEval = new List<double>();

        foreach (var comp in comps)
        {
            var bySubject = raw.Where(x => x.Competency == comp).GroupBy(x => x.SubjectUserId);

            var expectedAvgs = new List<decimal>();
            var evalAvgs = new List<decimal>();

            foreach (var g in bySubject)
            {
                var selfScores = g.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
                var othScores = g.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

                if (selfScores.Any()) expectedAvgs.Add(selfScores.Average());
                if (othScores.Any()) evalAvgs.Add(othScores.Average());
            }

            var expectedAvg = expectedAvgs.Any() ? expectedAvgs.Average() : 0m;
            var evalAvg = evalAvgs.Any() ? evalAvgs.Average() : 0m;

            var expectedPct = Math.Round((expectedAvg / 5m) * 100m, 0);
            var evalPct = Math.Round((evalAvg / 5m) * 100m, 0);

            teamLabels.Add(comp);
            teamExpected.Add((double)expectedPct);
            teamEval.Add((double)evalPct);
        }

        TeamLabelsJson = JsonSerializer.Serialize(teamLabels);
        TeamExpectedJson = JsonSerializer.Serialize(teamExpected);
        TeamEvalJson = JsonSerializer.Serialize(teamEval);
    }
}
