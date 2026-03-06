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
    public bool IsDueSoon { get; set; }
    public string SlaLabel { get; set; } = "En tiempo";
    public string SlaCss { get; set; } = "bg-success";

    public record AccessRow(string Source, string Label, string HostOrUrl, string Username, bool CanViewPassword, string PasswordPlain, string Notes);
    public List<AccessRow> AccessRows { get; set; } = new();
    public List<ClientServiceContract> LinkedContracts { get; set; } = new();

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
        IsDueSoon = Project.Status == ProjectStatus.Open && !IsOverdue && Project.EstimatedEndDate.Date <= DateTime.Today.AddDays(3);
        if (Project.Status == ProjectStatus.Closed)
        {
            SlaLabel = "Cerrado";
            SlaCss = "bg-secondary";
        }
        else if (IsOverdue)
        {
            SlaLabel = "SLA vencido";
            SlaCss = "bg-danger";
        }
        else if (IsDueSoon)
        {
            SlaLabel = "Por vencer";
            SlaCss = "bg-warning text-dark";
        }

        foreach (var a in Project.Accesses.OrderBy(x => x.Label))
        {
            var canView = isAdmin || (userId == Project.AssignedUserId);
            var plain = canView ? _protector.Unprotect(a.PasswordProtected) : "";

            AccessRows.Add(new AccessRow(
                "Proyecto",
                a.Label,
                a.HostOrUrl,
                a.Username,
                canView,
                plain,
                a.Notes
            ));
        }

        LinkedContracts = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(c => c.ProjectId == Project.Id)
            .OrderBy(c => c.ServiceType)
            .ThenBy(c => c.Label)
            .ToListAsync();

        foreach (var c in LinkedContracts.Where(x =>
                     !string.IsNullOrWhiteSpace(x.PortalUrl)
                     || !string.IsNullOrWhiteSpace(x.PortalUsername)
                     || !string.IsNullOrWhiteSpace(x.PortalPasswordProtected)))
        {
            var canView = isAdmin || (userId == Project.AssignedUserId);
            var plain = canView ? _protector.Unprotect(c.PortalPasswordProtected) : "";
            AccessRows.Add(new AccessRow(
                "Contrato",
                c.Label,
                c.PortalUrl,
                c.PortalUsername,
                canView,
                plain,
                $"Cuenta: {c.AccountNumber} · Contrato: {c.ContractNumber}"
            ));
        }

        return Page();
    }
}
