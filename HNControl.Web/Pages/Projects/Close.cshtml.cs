using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

[Authorize(Roles = AppRoles.Admin)]
public class CloseModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CloseModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ProjectId = id;
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        Title = p.Title;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == ProjectId);
        if (p == null) return NotFound();

        p.Status = ProjectStatus.Closed;
        p.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Projects/Details", new { id = ProjectId });
    }
}
