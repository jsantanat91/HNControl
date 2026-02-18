using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Eval360.Campaigns;

[Authorize(Roles = AppRoles.Admin)]
public class AssignmentsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public AssignmentsModel(ApplicationDbContext db) => _db = db;

    public Eval360Campaign Campaign { get; set; } = default!;

    public record Person(string UserId, string FullName, string Email);
    public List<Person> Participants { get; set; } = new();

    public int TotalAssignments { get; set; }
    public int SubmittedAssignments { get; set; }

    [BindProperty]
    public bool IncludeAdminLocal { get; set; } = false;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return RedirectToPage("/Admin/Eval360/Campaigns/Index");
        Campaign = c;

        await LoadAsync(id);
        return Page();
    }

    private async Task LoadAsync(Guid id)
    {
        // Participantes: por default excluimos el admin seed (admin@hn.local)
        var q = _db.EmployeeProfiles.AsNoTracking().OrderBy(e => e.FullName).AsQueryable();
        if (!IncludeAdminLocal)
            q = q.Where(e => !e.Email.ToLower().EndsWith("@hn.local"));

        Participants = await q.Select(e => new Person(e.UserId, e.FullName, e.Email)).ToListAsync();

        TotalAssignments = await _db.Eval360Assignments.CountAsync(a => a.CampaignId == id);
        SubmittedAssignments = await _db.Eval360Assignments.CountAsync(a => a.CampaignId == id && a.Status == Eval360AssignmentStatus.Submitted);
    }

    public async Task<IActionResult> OnPostGenerateAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return RedirectToPage("/Admin/Eval360/Campaigns/Index");
        Campaign = c;

        // participants
        var participants = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(e => IncludeAdminLocal || !e.Email.ToLower().EndsWith("@hn.local"))
            .OrderBy(e => e.FullName)
            .Select(e => new { e.UserId })
            .ToListAsync();

        var ids = participants.Select(x => x.UserId).ToList();
        if (ids.Count < 2 && !c.AllowSelf)
        {
            TempData["Msg"] = "Necesitas al menos 2 participantes (o habilitar autoevaluación).";
            await LoadAsync(id);
            return Page();
        }

        var existing = await _db.Eval360Assignments
            .AsNoTracking()
            .Where(a => a.CampaignId == id)
            .Select(a => new { a.EvaluatorUserId, a.SubjectUserId })
            .ToListAsync();

        var set = new HashSet<string>(existing.Select(x => $"{x.EvaluatorUserId}|{x.SubjectUserId}"));

        var toAdd = new List<Eval360Assignment>();

        foreach (var evaluator in ids)
        {
            foreach (var subject in ids)
            {
                var isSelf = evaluator == subject;
                if (isSelf && !c.AllowSelf) continue;

                var key = $"{evaluator}|{subject}";
                if (set.Contains(key)) continue;

                toAdd.Add(new Eval360Assignment
                {
                    CampaignId = id,
                    EvaluatorUserId = evaluator,
                    SubjectUserId = subject,
                    IsSelf = isSelf,
                    Status = Eval360AssignmentStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (toAdd.Any())
        {
            _db.Eval360Assignments.AddRange(toAdd);
            c.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Msg"] = $"Asignaciones creadas: {toAdd.Count}.";
        }
        else
        {
            TempData["Msg"] = "No había asignaciones nuevas por crear (ya estaba completo).";
        }

        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostResetAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return RedirectToPage("/Admin/Eval360/Campaigns/Index");
        Campaign = c;

        // Borra todo lo relacionado (FK cascade en answers/comments si el schema se creó como el script)
        var ass = await _db.Eval360Assignments.Where(a => a.CampaignId == id).ToListAsync();
        _db.Eval360Assignments.RemoveRange(ass);

        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Msg"] = "Asignaciones y respuestas borradas.";
        await LoadAsync(id);
        return Page();
    }
}
