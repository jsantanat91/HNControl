using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrderTemplates;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(Guid Id, string Type, string Name, bool IsActive, int ItemCount);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var list = await _db.ServiceOrderChecklistTemplates
            .Include(t => t.Items)
            .OrderBy(t => t.Type)
            .ToListAsync();

        Rows = list.Select(t => new Row(t.Id, t.Type.ToString(), t.Name, t.IsActive, t.Items.Count)).ToList();
    }
}
