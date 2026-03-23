using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Knowledge;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;

    public DetailsModel(ApplicationDbContext db, ISecretProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public KnowledgeLink? Doc { get; set; }
    public string SecretValue { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Doc = await _db.KnowledgeLinks
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Doc == null) return NotFound();

        Doc.ViewCount += 1;
        Doc.LastViewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (User.IsInRole(AppRoles.Admin))
            SecretValue = _protector.Unprotect(Doc.AccessSecretProtected);

        return Page();
    }

    public async Task<IActionResult> OnPostSetStatusAsync(Guid id, KnowledgeStatus status)
    {
        if (!User.IsInRole(AppRoles.Admin)) return Forbid();

        var doc = await _db.KnowledgeLinks.FirstOrDefaultAsync(x => x.Id == id);
        if (doc == null) return NotFound();

        doc.Status = status;
        if (status == KnowledgeStatus.Publicado && doc.PublishedAt == null)
            doc.PublishedAt = DateTime.UtcNow;

        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedByName = User.Identity?.Name ?? "admin";

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTogglePinAsync(Guid id)
    {
        if (!User.IsInRole(AppRoles.Admin)) return Forbid();

        var doc = await _db.KnowledgeLinks.FirstOrDefaultAsync(x => x.Id == id);
        if (doc == null) return NotFound();

        doc.IsPinned = !doc.IsPinned;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedByName = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }
}
