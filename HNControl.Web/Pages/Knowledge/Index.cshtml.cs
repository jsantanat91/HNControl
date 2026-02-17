using HNControl.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Knowledge;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record LinkRow(string Title, string Url, string Description, string CreatedAt);
    public record GroupRow(string Category, List<LinkRow> Links);

    public List<GroupRow> Grouped { get; set; } = new();

    public async Task OnGetAsync()
    {
        var links = await _db.KnowledgeLinks
            .OrderBy(l => l.Category)
            .ThenBy(l => l.Title)
            .ToListAsync();

        Grouped = links
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Category) ? "General" : l.Category)
            .Select(g => new GroupRow(
                g.Key,
                g.Select(l => new LinkRow(
                    l.Title,
                    l.Url,
                    l.Description,
                    l.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")
                )).ToList()
            ))
            .ToList();
    }
}
