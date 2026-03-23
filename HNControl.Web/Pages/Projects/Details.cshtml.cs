using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace HNControl.Web.Pages.Projects;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public DetailsModel(
        ApplicationDbContext db,
        ISecretProtector protector,
        IConfiguration cfg,
        IWebHostEnvironment env)
    {
        _db = db;
        _protector = protector;
        _cfg = cfg;
        _env = env;
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
    public record ActivityRow(Guid Id, string AssignedTo, string Description, int PlannedDays, int StartDay, int EndDay, double WidthPercent, double OffsetPercent);
    public List<ActivityRow> Activities { get; set; } = new();

    [BindProperty] public ActivityInput InputActivity { get; set; } = new();

    public class ActivityInput
    {
        public Guid ProjectId { get; set; }
        [StringLength(200)]
        public string AssignedTo { get; set; } = "";
        [StringLength(1000)]
        public string Description { get; set; } = "";
        [Range(1, 365)]
        public int PlannedDays { get; set; } = 1;
    }

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

        if (!isAdmin && userId != Project.AssignedUserId)
            return Forbid();

        ClientName = Project.Client?.Name ?? "";
        Responsible = Project.AssignedEmployee?.FullName ?? Project.AssignedUserId;
        IsOverdue = Project.Status == ProjectStatus.Open && Project.EstimatedEndDate < DateTime.UtcNow;
        IsDueSoon = Project.Status == ProjectStatus.Open && !IsOverdue && Project.EstimatedEndDate <= DateTime.UtcNow.AddHours(72);

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

        var nowUtc = DateTime.UtcNow;
        GanttTotalDays = Math.Max(1, (int)Math.Ceiling((Project.EstimatedEndDate - Project.StartDate).TotalHours));
        GanttElapsedDays = Math.Max(0, Math.Min(GanttTotalDays, (int)Math.Ceiling((nowUtc - Project.StartDate).TotalHours)));
        GanttProgressPercent = Math.Round((GanttElapsedDays * 100d) / GanttTotalDays, 1);
        try
        {
            Project.Activities = await _db.ProjectActivities
                .AsNoTracking()
                .Where(a => a.ProjectId == Project.Id)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
        }
        catch
        {
            Project.Activities = new List<ProjectActivity>();
        }
        BuildActivityGantt(Project);

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

    public async Task<IActionResult> OnPostAddActivityAsync()
    {
        if (!User.IsInRole(AppRoles.Admin))
            return Forbid();

        if (InputActivity.ProjectId == Guid.Empty)
            return RedirectToPage("/Projects/Index");

        var p = await _db.Projects
            .Include(x => x.AssignedEmployee)
            .FirstOrDefaultAsync(x => x.Id == InputActivity.ProjectId);
        if (p == null) return NotFound();

        if (string.IsNullOrWhiteSpace(InputActivity.Description))
            return RedirectToPage(new { id = InputActivity.ProjectId });

        int nextOrder;
        try
        {
            nextOrder = (await _db.ProjectActivities
                .Where(x => x.ProjectId == p.Id)
                .Select(x => (int?)x.SortOrder)
                .MaxAsync() ?? 0) + 1;
        }
        catch
        {
            nextOrder = 1;
        }
        _db.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = p.Id,
            AssignedToName = string.IsNullOrWhiteSpace(InputActivity.AssignedTo)
                ? (p.AssignedEmployee?.FullName ?? p.AssignedUserId)
                : InputActivity.AssignedTo.Trim(),
            Description = InputActivity.Description.Trim(),
            PlannedDays = Math.Max(1, InputActivity.PlannedDays),
            SortOrder = nextOrder
        });

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = InputActivity.ProjectId });
    }

    public async Task<IActionResult> OnPostDeleteActivityAsync(Guid id, Guid activityId)
    {
        if (!User.IsInRole(AppRoles.Admin))
            return Forbid();

        var activity = await _db.ProjectActivities.FirstOrDefaultAsync(x => x.Id == activityId && x.ProjectId == id);
        if (activity != null)
        {
            _db.ProjectActivities.Remove(activity);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
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

        var contracts = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(c => c.ProjectId == p.Id)
            .OrderBy(c => c.ServiceType)
            .ThenBy(c => c.Label)
            .ToListAsync();
        var projectActivities = await _db.ProjectActivities
            .AsNoTracking()
            .Where(a => a.ProjectId == p.Id)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync();

        var totalHours = Math.Max(1, (int)Math.Ceiling((p.EstimatedEndDate - p.StartDate).TotalHours));
        var elapsedHours = Math.Max(0, Math.Min(totalHours, (int)Math.Ceiling((DateTime.UtcNow - p.StartDate).TotalHours)));
        var percent = Math.Round((elapsedHours * 100d) / totalHours, 1);

        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var footer = (_cfg["Branding:ReportFooter"] ?? "HN Control").Trim();
        var logoBytes = LoadLogoBytes();
        var digitalHash = BuildProjectHash(p, contracts.Count, totalHours, elapsedHours, projectActivities.Count);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(company).FontSize(16).SemiBold();
                        c.Item().Text("Reporte de proyecto").FontSize(12).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(p.Title).FontSize(12).SemiBold();
                    });

                    r.ConstantItem(160).AlignRight().AlignMiddle().Element(el =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                            el.Height(56).Width(160).Image(logoBytes).FitArea();
                        else
                            el.Text("LOGO").FontSize(14).FontColor(Colors.Grey.Darken2);
                    });
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Cliente").SemiBold();
                            cc.Item().Text(p.Client?.Name ?? "-");
                            if (!string.IsNullOrWhiteSpace(p.Client?.Email))
                                cc.Item().Text(p.Client!.Email).FontColor(Colors.Grey.Darken2);
                        });

                        r.ConstantItem(270).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Datos del proyecto").SemiBold();
                            cc.Item().Text($"Responsable: {p.AssignedEmployee?.FullName ?? p.AssignedUserId}");
                            cc.Item().Text($"Estado: {(p.Status == ProjectStatus.Closed ? "Cerrado" : "Abierto")}");
                            cc.Item().Text($"Inicio: {p.StartDate.ToLocalTime():yyyy-MM-dd HH:mm}");
                            cc.Item().Text($"Fin estimado: {p.EstimatedEndDate.ToLocalTime():yyyy-MM-dd HH:mm}");
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Diagrama Gantt (calendario)").SemiBold();
                        cc.Item().PaddingTop(6).Text($"Avance por tiempo: {percent}% ({elapsedHours}/{totalHours} horas)").FontColor(Colors.Grey.Darken2);

                        cc.Item().PaddingTop(8).Height(22).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Layers(l =>
                        {
                            l.Layer().Background(Colors.Grey.Lighten5);
                            l.PrimaryLayer().Width((float)Math.Max(1, percent)).Background(Colors.Blue.Medium);
                        });

                        cc.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text($"Inicio: {p.StartDate.ToLocalTime():yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken2);
                            r.RelativeItem().AlignCenter().Text($"Duracion: {totalHours} h").FontSize(9).FontColor(Colors.Grey.Darken2);
                            r.RelativeItem().AlignRight().Text($"Cierre estimado: {p.EstimatedEndDate.ToLocalTime():yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Plan de actividades").SemiBold();
                        cc.Item().PaddingTop(6);
                        if (projectActivities.Count == 0)
                        {
                            cc.Item().Text("Sin actividades capturadas.").FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            var totalPlanDays = Math.Max(1, projectActivities.Sum(a => Math.Max(1, a.PlannedDays)));
                            var current = 0;
                            foreach (var a in projectActivities)
                            {
                                var days = Math.Max(1, a.PlannedDays);
                                var offset = Math.Round((current * 100d) / totalPlanDays, 1);
                                var width = Math.Round((days * 100d) / totalPlanDays, 1);
                                var start = current + 1;
                                var end = current + days;
                                current += days;

                                cc.Item().PaddingBottom(4).Column(row =>
                                {
                                    var blocks = Math.Max(1, (int)Math.Round((days * 30d) / totalPlanDays));
                                    var timeline = new string('·', Math.Max(0, start - 1)) + new string('█', blocks);
                                    row.Item().Text($"{a.AssignedToName}: {a.Description} ({days} dias)").FontColor(Colors.Grey.Darken2);
                                    row.Item().Text(timeline).FontSize(9).FontColor(Colors.Blue.Medium);
                                    row.Item().Text($"Dias {start}-{end}").FontSize(8).FontColor(Colors.Grey.Darken2);
                                });
                            }
                        }
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Resumen tecnico").SemiBold();
                        cc.Item().PaddingTop(6).Text($"Objetivo: {Safe(p.Objective)}");
                        cc.Item().Text($"Alcance: {Safe(p.Scope)}");
                        cc.Item().Text($"Descripcion: {Safe(p.ActivityDescription)}");
                        cc.Item().Text($"Comentarios: {Safe(p.AdditionalComments)}");
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Contratos ligados").SemiBold();
                        cc.Item().PaddingTop(6);

                        if (contracts.Count == 0)
                        {
                            cc.Item().Text("Sin contratos ligados al proyecto.").FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            foreach (var c in contracts.Take(8))
                            {
                                cc.Item().Text($"- {c.Label} [{c.ServiceType}] - Cuenta {c.AccountNumber}").FontColor(Colors.Grey.Darken2);
                            }

                            if (contracts.Count > 8)
                                cc.Item().Text($"+ {contracts.Count - 8} contratos mas").FontColor(Colors.Grey.Darken2);
                        }
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Validacion").SemiBold();
                        cc.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                            {
                                x.Item().Text("Responsable tecnico").SemiBold();
                                x.Item().Text(p.AssignedEmployee?.FullName ?? p.AssignedUserId).FontColor(Colors.Grey.Darken2);
                                x.Item().PaddingTop(20).Text("Firma: ________________________").FontColor(Colors.Grey.Darken2);
                            });

                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                            {
                                x.Item().Text("Cliente / Aprobacion").SemiBold();
                                x.Item().Text(p.Client?.Name ?? "-").FontColor(Colors.Grey.Darken2);
                                x.Item().PaddingTop(20).Text("Firma: ________________________").FontColor(Colors.Grey.Darken2);
                            });
                        });
                    });
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"{footer} - {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    f.Item().AlignCenter().Text($"Firma digital: {digitalHash}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        var fileName = $"Proyecto-{p.Title.Replace(' ', '_')}-{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private static string Safe(string? text)
        => string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();

    private byte[]? LoadLogoBytes()
    {
        var configured = (_cfg["Branding:LogoPath"] ?? "").Trim();
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(configured);

        candidates.Add("assets/logo.png");
        candidates.Add("wwwroot/assets/logo.png");

        if (!string.IsNullOrWhiteSpace(_env.ContentRootPath))
        {
            candidates.Add(Path.Combine(_env.ContentRootPath, "assets", "logo.png"));
            candidates.Add(Path.Combine(_env.ContentRootPath, "wwwroot", "assets", "logo.png"));
        }

        if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
            candidates.Add(Path.Combine(_env.WebRootPath, "assets", "logo.png"));

        foreach (var raw in candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var abs in ExpandToAbsolutePaths(raw))
            {
                try
                {
                    if (System.IO.File.Exists(abs))
                        return System.IO.File.ReadAllBytes(abs);
                }
                catch
                {
                    // no-op: no tumbar el PDF si falla el logo
                }
            }
        }

        return null;
    }

    private IEnumerable<string> ExpandToAbsolutePaths(string path)
    {
        path = path.Replace("/", Path.DirectorySeparatorChar.ToString());
        if (Path.IsPathRooted(path))
            return new[] { path };

        var bases = new List<string>();
        if (!string.IsNullOrWhiteSpace(_env.ContentRootPath)) bases.Add(_env.ContentRootPath);
        if (!string.IsNullOrWhiteSpace(_env.WebRootPath)) bases.Add(_env.WebRootPath);
        bases.Add(AppContext.BaseDirectory);
        bases.Add(Directory.GetCurrentDirectory());

        return bases.Select(b => Path.GetFullPath(Path.Combine(b, path)));
    }

    private static string BuildProjectHash(Project p, int contractsCount, int totalHours, int elapsedHours, int activitiesCount)
    {
        var payload = string.Join("|",
            p.Id,
            p.ClientId,
            p.AssignedUserId,
            p.Status,
            p.StartDate.ToUniversalTime().ToString("yyyyMMddHHmmss"),
            p.EstimatedEndDate.ToUniversalTime().ToString("yyyyMMddHHmmss"),
            p.Title ?? "",
            p.Objective ?? "",
            p.ActivityDescription ?? "",
            contractsCount,
            totalHours,
            elapsedHours,
            activitiesCount);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..16];
    }

    private void BuildActivityGantt(Project p)
    {
        var acts = (p.Activities ?? new List<ProjectActivity>())
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.CreatedAt)
            .ToList();

        if (!acts.Any())
        {
            Activities = new List<ActivityRow>();
            return;
        }

        var totalDays = Math.Max(1, acts.Sum(a => Math.Max(1, a.PlannedDays)));
        var cursor = 0;
        Activities = new List<ActivityRow>();

        foreach (var a in acts)
        {
            var days = Math.Max(1, a.PlannedDays);
            var start = cursor + 1;
            var end = cursor + days;
            var offset = (cursor * 100d) / totalDays;
            var width = (days * 100d) / totalDays;
            cursor += days;

            Activities.Add(new ActivityRow(
                a.Id,
                string.IsNullOrWhiteSpace(a.AssignedToName) ? "-" : a.AssignedToName,
                string.IsNullOrWhiteSpace(a.Description) ? "-" : a.Description,
                days,
                start,
                end,
                Math.Round(width, 2),
                Math.Round(offset, 2)
            ));
        }
    }
}
