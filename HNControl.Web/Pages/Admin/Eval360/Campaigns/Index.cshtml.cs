using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Eval360.Campaigns;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(
        Guid Id,
        string Title,
        Eval360CampaignStatus Status,
        DateTime? PeriodStart,
        DateTime? PeriodEnd,
        bool AllowSelf,
        bool ResultsVisibleToEmployee,
        int AssignmentsTotal,
        int AssignmentsSubmitted
    );

    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var campaigns = await _db.Eval360Campaigns
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        if (!campaigns.Any())
            return;

        var ids = campaigns.Select(x => x.Id).ToList();

        var agg = await _db.Eval360Assignments
            .AsNoTracking()
            .Where(a => ids.Contains(a.CampaignId))
            .GroupBy(a => a.CampaignId)
            .Select(g => new
            {
                CampaignId = g.Key,
                Total = g.Count(),
                Submitted = g.Count(x => x.Status == Eval360AssignmentStatus.Submitted)
            })
            .ToListAsync();

        var map = agg.ToDictionary(x => x.CampaignId, x => x);

        foreach (var c in campaigns)
        {
            map.TryGetValue(c.Id, out var a);
            Rows.Add(new Row(
                c.Id,
                c.Title,
                c.Status,
                c.PeriodStart,
                c.PeriodEnd,
                c.AllowSelf,
                c.ResultsVisibleToEmployee,
                a?.Total ?? 0,
                a?.Submitted ?? 0
            ));
        }
    }

    public async Task<IActionResult> OnPostOpenAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return RedirectToPage();

        c.Status = Eval360CampaignStatus.Open;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloseAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return RedirectToPage();

        c.Status = Eval360CampaignStatus.Closed;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var c = await _db.Eval360Campaigns.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return RedirectToPage();

        _db.Eval360Campaigns.Remove(c);
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }
}
