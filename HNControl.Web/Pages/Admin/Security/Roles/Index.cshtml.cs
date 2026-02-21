using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Row> Roles { get; set; } = new();

    public class Row
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int ModulesCount { get; set; }
    }

    public async Task OnGetAsync()
    {
        Roles = await _db.PermissionRoles
            .AsNoTracking()
            .Select(r => new Row
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsDefault = r.IsDefault,
                IsActive = r.IsActive,
                ModulesCount = r.Modules.Count
            })
            .OrderByDescending(r => r.IsDefault)
            .ThenByDescending(r => r.IsActive)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }
}
