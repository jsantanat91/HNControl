using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<InventoryItem> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = (Q ?? "").Trim();
        var query = _db.InventoryItems.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Name).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var l = q.ToLower();
            query = query.Where(i =>
                (i.Name ?? "").ToLower().Contains(l) ||
                (i.Sku ?? "").ToLower().Contains(l));
        }

        Items = await query.Take(500).ToListAsync();
    }
}
