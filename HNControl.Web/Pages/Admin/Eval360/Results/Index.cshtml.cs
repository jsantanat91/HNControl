using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    public record Row(string Competency, decimal AutoPct, decimal OthersPct, decimal DiffPct, string LevelAuto, string LevelOthers);
    public List<Row> Rows { get; set; } = new();

    public string LabelsJson { get; set; } = "[]";
    public string AutoJson { get; set; } = "[]";
    public string OthersJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
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
        return Page();
    }

    private static string Level(decimal pct)
    {
        if (pct < 50) return "Crítico";
        if (pct < 75) return "Alto";
        if (pct < 85) return "Moderado";
        return "Desarrollado";
    }

    private async Task BuildRowsAsync(Guid campaignId, string subjectUserId)
    {
        // Trae promedios por competencia para auto vs evaluadores
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

            decimal autoAvg = 0m;
            decimal otherAvg = 0m;

            var aSelf = compAnswers.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
            var aOth = compAnswers.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

            if (aSelf.Any()) autoAvg = aSelf.Average();
            if (aOth.Any()) otherAvg = aOth.Average();

            var autoPct = Math.Round((autoAvg / 5m) * 100m, 0);
            var othPct = Math.Round((otherAvg / 5m) * 100m, 0);
            var diffPct = Math.Round(autoPct - othPct, 0);

            Rows.Add(new Row(comp, autoPct, othPct, diffPct, Level(autoPct), Level(othPct)));
        }

        LabelsJson = JsonSerializer.Serialize(Rows.Select(r => r.Competency));
        AutoJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.AutoPct));
        OthersJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.OthersPct));
    }
}
