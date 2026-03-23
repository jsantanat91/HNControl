using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

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
    public int GanttTotalDays { get; set; }
    public int GanttElapsedDays { get; set; }
    public double GanttProgressPercent { get; set; }

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

        GanttTotalDays = Math.Max(1, (Project.EstimatedEndDate.Date - Project.StartDate.Date).Days + 1);
        GanttElapsedDays = Math.Min(GanttTotalDays, Math.Max(0, (DateTime.Today - Project.StartDate.Date).Days + 1));
        GanttProgressPercent = Math.Round((GanttElapsedDays * 100d) / GanttTotalDays, 1);

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

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var p = await _db.Projects
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.AssignedEmployee)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound();
        if (!isAdmin && userId != p.AssignedUserId) return Forbid();

        var totalDays = Math.Max(1, (p.EstimatedEndDate.Date - p.StartDate.Date).Days + 1);
        var elapsed = Math.Min(totalDays, Math.Max(0, (DateTime.Today - p.StartDate.Date).Days + 1));
        var percent = Math.Round((elapsed * 100d) / totalDays, 1);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(22);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Reporte de proyecto").FontSize(16).SemiBold();
                    col.Item().Text($"{p.Title} · {p.Client?.Name ?? "-"}").FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Responsable: {p.AssignedEmployee?.FullName ?? p.AssignedUserId}");
                    col.Item().Text($"Inicio: {p.StartDate:yyyy-MM-dd} · Fin estimado: {p.EstimatedEndDate:yyyy-MM-dd}");
                    col.Item().Text($"Estatus: {p.Status} · Avance calendario: {percent}% ({elapsed}/{totalDays} días)");
                    col.Item().Text($"Objetivo: {p.Objective}");
                    col.Item().Text($"Alcance: {p.Scope}");
                    col.Item().Text($"Descripción: {p.ActivityDescription}");
                    col.Item().Text($"Comentarios: {p.AdditionalComments}");
                });
            });
        }).GeneratePdf();

        var fileName = $"Proyecto-{p.Title.Replace(' ', '_')}-{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }
}
