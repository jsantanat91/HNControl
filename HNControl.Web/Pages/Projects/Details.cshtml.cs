using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Globalization;
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
    public record PdfGanttRow(string Task, string AssignedTo, DateTime StartDate, DateTime EndDate, int Days, double ProgressPercent, double OffsetPercent, double WidthPercent, int ColorIndex);
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
        var totalPlanDays = Math.Max(1, projectActivities.Sum(a => Math.Max(1, a.PlannedDays)));
        var planRows = BuildPdfGanttRows(p.StartDate.ToLocalTime().Date, projectActivities, totalPlanDays, DateTime.UtcNow.ToLocalTime());
        const float timelineWidth = 300f;

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
                        cc.Item().PaddingTop(8);
                        if (planRows.Count == 0)
                        {
                            cc.Item().Text("Sin actividades capturadas.").FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            cc.Item().Row(header =>
                            {
                                header.ConstantItem(84).Text("Inicio").SemiBold().FontSize(9);
                                header.ConstantItem(84).Text("Fin").SemiBold().FontSize(9);
                                header.ConstantItem(38).Text("Dias").SemiBold().FontSize(9);
                                header.ConstantItem(54).AlignCenter().Text("Avance").SemiBold().FontSize(9);
                                header.ConstantItem(240).Text("Tarea").SemiBold().FontSize(9);
                                header.ConstantItem(timelineWidth).Text($"Timeline ({totalPlanDays} dias)").SemiBold().FontSize(9);
                            });

                            cc.Item().PaddingTop(3).LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten2);
                            cc.Item().PaddingTop(3).Row(axis =>
                            {
                                axis.ConstantItem(84 + 84 + 38 + 54 + 240).Text("");
                                axis.ConstantItem(timelineWidth).Text($"D1  ...  D{Math.Max(1, totalPlanDays / 2)}  ...  D{totalPlanDays}")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            });

                            foreach (var row in planRows)
                            {
                                cc.Item().PaddingTop(2).Row(r =>
                                {
                                    r.ConstantItem(84).Text(row.StartDate.ToString("dd/MM/yyyy")).FontSize(8.5f);
                                    r.ConstantItem(84).Text(row.EndDate.ToString("dd/MM/yyyy")).FontSize(8.5f);
                                    r.ConstantItem(38).Text(row.Days.ToString()).FontSize(8.5f);
                                    r.ConstantItem(54).AlignCenter().Text($"{Math.Round(row.ProgressPercent)}%").FontSize(8.5f);
                                    r.ConstantItem(240).Column(task =>
                                    {
                                        task.Item().Text(row.Task).FontSize(8.5f);
                                        task.Item().Text(row.AssignedTo).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                                    });

                                    r.ConstantItem(timelineWidth).Height(16).Border(0.6f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Layers(l =>
                                    {
                                        l.Layer().Background(Colors.Grey.Lighten5);
                                        l.PrimaryLayer().PaddingLeft((float)(timelineWidth * row.OffsetPercent / 100d)).Element(bar =>
                                        {
                                            bar.Width((float)Math.Max(2d, timelineWidth * row.WidthPercent / 100d))
                                                .Height(16)
                                                .Background(GetGanttColor(row.ColorIndex))
                                                .AlignMiddle()
                                                .AlignCenter()
                                                .Text($"{Math.Round(row.ProgressPercent)}%")
                                                .FontSize(7)
                                                .FontColor(Colors.White);
                                        });
                                    });
                                });
                            }

                            cc.Item().PaddingTop(10).Text("Dependencias entre tareas (secuencial)").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                            var depHeight = Math.Max(60, 26 + (planRows.Count * 24));
                            var depSvg = BuildDependencySvg(planRows, timelineWidth, depHeight);
                            cc.Item().PaddingTop(4).Height(depHeight).Svg(depSvg);
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

    private static List<PdfGanttRow> BuildPdfGanttRows(DateTime planStartLocal, List<ProjectActivity> activities, int totalPlanDays, DateTime nowLocal)
    {
        var rows = new List<PdfGanttRow>();
        if (activities.Count == 0)
            return rows;

        var cursor = 0;
        var colorIndex = 0;

        foreach (var a in activities)
        {
            var days = Math.Max(1, a.PlannedDays);
            var startDay = cursor + 1;
            var endDay = cursor + days;
            var startDate = planStartLocal.AddDays(startDay - 1);
            var endDate = planStartLocal.AddDays(endDay - 1);
            var offset = (cursor * 100d) / totalPlanDays;
            var width = (days * 100d) / totalPlanDays;
            cursor += days;

            var progress = CalcProgress(nowLocal.Date, startDate, endDate);
            rows.Add(new PdfGanttRow(
                string.IsNullOrWhiteSpace(a.Description) ? "-" : a.Description.Trim(),
                string.IsNullOrWhiteSpace(a.AssignedToName) ? "-" : a.AssignedToName.Trim(),
                startDate,
                endDate,
                days,
                progress,
                Math.Round(offset, 2),
                Math.Round(width, 2),
                colorIndex++));
        }

        return rows;
    }

    private static double CalcProgress(DateTime nowDate, DateTime startDate, DateTime endDate)
    {
        if (nowDate < startDate.Date)
            return 0;
        if (nowDate >= endDate.Date)
            return 100;

        var total = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
        var elapsed = Math.Max(0, (nowDate - startDate.Date).Days + 1);
        return Math.Round(Math.Clamp((elapsed * 100d) / total, 0, 100), 1);
    }

    private static string GetGanttColor(int index)
    {
        var colors = new[]
        {
            Colors.Blue.Medium,
            Colors.Green.Medium,
            Colors.Purple.Medium,
            Colors.Orange.Medium,
            Colors.Cyan.Darken2
        };

        return colors[Math.Abs(index) % colors.Length];
    }

    private static string BuildDependencySvg(IReadOnlyList<PdfGanttRow> rows, float timelineWidth, int canvasHeight)
    {
        var sb = new StringBuilder();
        var w = (int)Math.Max(320, Math.Ceiling(timelineWidth));
        var h = Math.Max(60, canvasHeight);
        const int rowH = 24;
        const int top = 10;

        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>");
        sb.Append("<defs>");
        sb.Append("<marker id='arrowHead' markerWidth='7' markerHeight='7' refX='6' refY='3.5' orient='auto'>");
        sb.Append("<polygon points='0 0, 7 3.5, 0 7' fill='#6B7280'/>");
        sb.Append("</marker>");
        sb.Append("</defs>");
        sb.Append($"<rect x='0' y='0' width='{w}' height='{h}' fill='#F8FAFC' stroke='#E5E7EB'/>");

        // Guías verticales para lectura temporal
        for (var i = 0; i <= 10; i++)
        {
            var gx = (w * i) / 10.0;
            sb.Append($"<line x1='{F(gx)}' y1='0' x2='{F(gx)}' y2='{h}' stroke='#E5E7EB' stroke-width='0.8'/>");
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var y = top + (i * rowH);
            var barY = y + 5;
            var barH = 12;
            var x = Math.Max(1d, (w * row.OffsetPercent / 100d));
            var barW = Math.Max(6d, (w * row.WidthPercent / 100d));
            var color = GetGanttHexColor(row.ColorIndex);
            var progressW = Math.Max(2d, barW * Math.Clamp(row.ProgressPercent, 0, 100) / 100d);

            sb.Append($"<rect x='{F(x)}' y='{F(barY)}' rx='5' ry='5' width='{F(barW)}' height='{barH}' fill='{color}' opacity='0.85'/>");
            sb.Append($"<rect x='{F(x)}' y='{F(barY)}' rx='5' ry='5' width='{F(progressW)}' height='{barH}' fill='rgba(17,24,39,0.22)'/>");

            if (i > 0)
            {
                var prev = rows[i - 1];
                var prevY = top + ((i - 1) * rowH) + 11;
                var prevEndX = Math.Max(1d, (w * (prev.OffsetPercent + prev.WidthPercent) / 100d));
                var currentStartX = Math.Max(1d, x);
                var midX = Math.Max(2d, prevEndX + 8d);

                // Conector tipo "finish-to-start" con flecha
                sb.Append(
                    $"<polyline points='{F(prevEndX)},{F(prevY)} {F(midX)},{F(prevY)} {F(midX)},{F(barY + barH / 2d)} {F(currentStartX - 3d)},{F(barY + barH / 2d)}' " +
                    "fill='none' stroke='#6B7280' stroke-width='1.2' marker-end='url(#arrowHead)'/>");
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string GetGanttHexColor(int index)
    {
        var colors = new[]
        {
            "#3B82F6", // blue
            "#22C55E", // green
            "#A855F7", // purple
            "#F59E0B", // amber
            "#06B6D4"  // cyan
        };
        return colors[Math.Abs(index) % colors.Length];
    }
}
