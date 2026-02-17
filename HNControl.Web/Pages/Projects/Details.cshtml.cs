using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;

    public DetailsModel(ApplicationDbContext db, ISecretProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public Project? Project { get; set; }
    public string ClientName { get; set; } = "";
    public string Responsible { get; set; } = "";
    public bool IsOverdue { get; set; }

    public record AccessRow(string Label, string HostOrUrl, string Username, bool CanViewPassword, string PasswordPlain);
    public List<AccessRow> AccessRows { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        Project = await _db.Projects
            .Include(p => p.Client)
            .Include(p => p.AssignedEmployee)
            .Include(p => p.Accesses)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Project == null) return NotFound();

        // Permisos: admin ve todo; empleado solo si es responsable
        if (!isAdmin && userId != Project.AssignedUserId)
            return Forbid();

        ClientName = Project.Client?.Name ?? "";
        Responsible = Project.AssignedEmployee?.FullName ?? Project.AssignedUserId;
        IsOverdue = Project.Status == ProjectStatus.Open && Project.EstimatedEndDate.Date < DateTime.Today;

        foreach (var a in Project.Accesses.OrderBy(x => x.Label))
        {
            var canView = isAdmin || (userId == Project.AssignedUserId);
            var plain = canView ? _protector.Unprotect(a.PasswordProtected) : "";

            AccessRows.Add(new AccessRow(
                a.Label,
                a.HostOrUrl,
                a.Username,
                canView,
                plain
            ));
        }

        return Page();
    }
}
