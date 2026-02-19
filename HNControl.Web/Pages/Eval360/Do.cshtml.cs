using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Eval360;

[Authorize]
public class DoModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DoModel(ApplicationDbContext db) => _db = db;

    public Eval360Assignment Assignment { get; set; } = default!;
    public EmployeeProfile Subject { get; set; } = default!;
    public Eval360Campaign Campaign { get; set; } = default!;

    public record Q(Guid Id, string Text);
    public record Comp(Guid Id, string Name, List<Q> Questions);

    public List<Comp> Competencies { get; set; } = new();

    public Dictionary<Guid, int> ExistingScores { get; set; } = new();
    public Dictionary<Guid, string> ExistingComments { get; set; } = new();

    public string ScaleLeft { get; set; } = "Crítico (muy por debajo)";
    public string ScaleLeftMid { get; set; } = "Necesita mejorar";
    public string ScaleMid { get; set; } = "Cumple";
    public string ScaleRightMid { get; set; } = "Bueno";
    public string ScaleRight { get; set; } = "Excelente";


    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var ass = await _db.Eval360Assignments
            .Include(a => a.Campaign)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (ass == null) return NotFound();

        var isAdmin = User.IsInRole(AppRoles.Admin);
        if (!isAdmin && ass.EvaluatorUserId != userId) return Forbid();

        // campaña abierta o admin
        if (!isAdmin && ass.Campaign!.Status != Eval360CampaignStatus.Open) return Forbid();

        Assignment = ass;
        Campaign = ass.Campaign!;

        Subject = await _db.EmployeeProfiles.AsNoTracking().FirstAsync(e => e.UserId == ass.SubjectUserId);

        // marca inicio (solo la primera vez)
        if (ass.StartedAt == null)
        {
            ass.StartedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        await LoadCatalogAsync();

        // prefills
        ExistingScores = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.AssignmentId == id)
            .ToDictionaryAsync(a => a.QuestionId, a => a.Score);

        ExistingComments = await _db.Eval360Comments
            .AsNoTracking()
            .Where(c => c.AssignmentId == id)
            .ToDictionaryAsync(c => c.CompetencyId, c => c.CommentText);

        return Page();
    }

    private async Task LoadCatalogAsync()
    {
        var comps = await _db.Eval360Competencies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Questions = c.Questions.Where(q => q.IsActive).OrderBy(q => q.SortOrder).Select(q => new { q.Id, q.Text }).ToList()
            })
            .ToListAsync();

        Competencies = comps.Select(c => new Comp(
            c.Id,
            c.Name,
            c.Questions.Select(q => new Q(q.Id, q.Text)).ToList()
        )).ToList();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var ass = await _db.Eval360Assignments
            .Include(a => a.Campaign)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (ass == null) return NotFound();

        var isAdmin = User.IsInRole(AppRoles.Admin);
        if (!isAdmin && ass.EvaluatorUserId != userId) return Forbid();
        if (!isAdmin && ass.Campaign!.Status != Eval360CampaignStatus.Open) return Forbid();

        Assignment = ass;
        Campaign = ass.Campaign!;
        Subject = await _db.EmployeeProfiles.AsNoTracking().FirstAsync(e => e.UserId == ass.SubjectUserId);

        await LoadCatalogAsync();

        // parse answers
        var scores = new Dictionary<Guid, int>();
        foreach (var comp in Competencies)
        {
            foreach (var q in comp.Questions)
            {
                var key = $"q_{q.Id}";
                var raw = Request.Form[key].ToString();
                if (!int.TryParse(raw, out var val) || val < 1 || val > 5)
                {
                    ModelState.AddModelError("", $"Falta responder: {comp.Name} · {q.Text}");
                }
                else
                {
                    scores[q.Id] = val;
                }
            }
        }

        if (!ModelState.IsValid)
        {
            // reload existing for sticky UI
            ExistingScores = scores;
            ExistingComments = Competencies.ToDictionary(c => c.Id, c => Request.Form[$"c_{c.Id}"].ToString());
            return Page();
        }

        // Upsert simple: borrar y reinsertar
        var prevAns = await _db.Eval360Answers.Where(a => a.AssignmentId == id).ToListAsync();
        _db.Eval360Answers.RemoveRange(prevAns);

        var prevCom = await _db.Eval360Comments.Where(c => c.AssignmentId == id).ToListAsync();
        _db.Eval360Comments.RemoveRange(prevCom);

        foreach (var kv in scores)
        {
            _db.Eval360Answers.Add(new Eval360Answer
            {
                AssignmentId = id,
                QuestionId = kv.Key,
                Score = kv.Value,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var comp in Competencies)
        {
            var ck = $"c_{comp.Id}";
            var txt = (Request.Form[ck].ToString() ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(txt))
            {
                _db.Eval360Comments.Add(new Eval360Comment
                {
                    AssignmentId = id,
                    CompetencyId = comp.Id,
                    CommentText = txt,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        ass.Status = Eval360AssignmentStatus.Submitted;
        ass.SubmittedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Msg"] = "Evaluación guardada ✅";
        return RedirectToPage("/Eval360/Index");
    }
}
