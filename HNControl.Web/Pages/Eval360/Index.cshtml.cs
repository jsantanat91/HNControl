using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Eval360;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record PendingRow(Guid AssignmentId, Guid CampaignId, string CampaignTitle, string SubjectName, bool IsSelf, DateTime? PeriodEnd);
    public List<PendingRow> Pending { get; set; } = new();

    public record ResultRow(Guid CampaignId, string CampaignTitle, DateTime? PeriodEnd);
    public List<ResultRow> AvailableResults { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return;

        // pendientes
        var pending = await _db.Eval360Assignments
            .AsNoTracking()
            .Where(a => a.EvaluatorUserId == userId
                        && a.Status == Eval360AssignmentStatus.Pending
                        && a.Campaign!.Status == Eval360CampaignStatus.Open)
            .OrderBy(a => a.Campaign!.CreatedAt)
            .ThenBy(a => a.SubjectUserId)
            .Select(a => new
            {
                a.Id,
                a.CampaignId,
                CampaignTitle = a.Campaign!.Title,
                a.SubjectUserId,
                a.IsSelf,
                PeriodEnd = a.Campaign!.PeriodEnd
            })
            .ToListAsync();

        if (pending.Any())
        {
            var subjectIds = pending.Select(x => x.SubjectUserId).Distinct().ToList();
            var names = await _db.EmployeeProfiles
                .AsNoTracking()
                .Where(e => subjectIds.Contains(e.UserId))
                .Select(e => new { e.UserId, e.FullName })
                .ToListAsync();
            var map = names.ToDictionary(x => x.UserId, x => x.FullName);

            Pending = pending.Select(x =>
            {
                map.TryGetValue(x.SubjectUserId, out var name);
                name ??= x.SubjectUserId;
                return new PendingRow(x.Id, x.CampaignId, x.CampaignTitle, name, x.IsSelf, x.PeriodEnd);
            }).ToList();
        }

        // resultados (cuando está cerrada y visible para empleado)
        var res = await _db.Eval360Campaigns
            .AsNoTracking()
            .Where(c => c.Status == Eval360CampaignStatus.Closed && c.ResultsVisibleToEmployee)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ResultRow(c.Id, c.Title, c.PeriodEnd))
            .ToListAsync();

        AvailableResults = res;
    }
}
