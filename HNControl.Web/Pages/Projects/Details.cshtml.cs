using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Data;
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
    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }
    public string ClientName { get; set; } = "";
    public string Responsible { get; set; } = "";
    public bool IsOverdue { get; set; }
    public bool IsDueSoon { get; set; }
    public string SlaLabel { get; set; } = "En tiempo";
    public string SlaCss { get; set; } = "bg-success";
    public int GanttTotalDays { get; set; }
    public int GanttElapsedDays { get; set; }
    public double GanttProgressPercent { get; set; }
    public record ActivityRow(Guid Id, string AssignedTo, string Description, int PlannedHours, string DurationLabel, int StartHour, int EndHour, string StartText, string EndText, double WidthPercent, double OffsetPercent, string ColorHex, bool IsCompleted, string CompletedText);
    public record PdfGanttRow(string Task, string AssignedTo, DateTime StartDate, DateTime EndDate, int Hours, string DurationLabel, double ProgressPercent, double OffsetPercent, double WidthPercent, int ColorIndex, bool IsCompleted, string CompletedText);
    public List<ActivityRow> Activities { get; set; } = new();
    public List<EmployeeOptionVm> EmployeeOptions { get; set; } = new();

    [BindProperty] public ActivityInput InputActivity { get; set; } = new();

    public class ActivityInput
    {
        public Guid ProjectId { get; set; }
        [StringLength(64)]
        public string? AssignedToUserId { get; set; }
        [StringLength(200)]
        public string AssignedTo { get; set; } = "";
        [StringLength(1000)]
        public string Description { get; set; } = "";
        [Range(1, 3650)]
        public int DurationValue { get; set; } = 1;
        [Required]
        public string DurationUnit { get; set; } = "hours";
        public DateTime? StartDateLocal { get; set; }
        public DateTime? EndDateLocal { get; set; }
    }
    public record EmployeeOptionVm(string UserId, string FullName);

    public record AccessRow(string Source, string Label, string HostOrUrl, string Username, bool CanViewPassword, string PasswordPlain, string Notes);
    public List<AccessRow> AccessRows { get; set; } = new();
    public List<ClientServiceContract> LinkedContracts { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await EnsureProjectActivityColumnsAsync();

        var isAdmin = AppRoles.IsGlobalAdmin(User);
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

        Project.Activities = await LoadProjectActivitiesSafeAsync(Project.Id);
        GanttTotalDays = Math.Max(1, Project.Activities.Sum(a => Math.Max(1, a.PlannedDays)));
        GanttElapsedDays = Math.Max(0, Project.Activities.Where(a => a.IsCompleted).Sum(a => Math.Max(1, a.PlannedDays)));
        GanttProgressPercent = Math.Round((GanttElapsedDays * 100d) / GanttTotalDays, 1);
        BuildActivityGantt(Project);

        EmployeeOptions = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new EmployeeOptionVm(x.UserId, x.FullName))
            .ToListAsync();

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
        if (!AppRoles.IsGlobalAdmin(User))
            return Forbid();

        await EnsureProjectActivityColumnsAsync();

        if (InputActivity.ProjectId == Guid.Empty)
            return RedirectToPage("/Projects/Index");

        var p = await _db.Projects
            .Include(x => x.AssignedEmployee)
            .FirstOrDefaultAsync(x => x.Id == InputActivity.ProjectId);
        if (p == null) return NotFound();

        if (string.IsNullOrWhiteSpace(InputActivity.Description))
            return RedirectToPage(new { id = InputActivity.ProjectId });

        if (InputActivity.StartDateLocal.HasValue && InputActivity.EndDateLocal.HasValue
            && InputActivity.EndDateLocal.Value.Date < InputActivity.StartDateLocal.Value.Date)
        {
            (InputActivity.StartDateLocal, InputActivity.EndDateLocal) = (InputActivity.EndDateLocal, InputActivity.StartDateLocal);
        }

        var nextOrder = await GetNextActivitySortOrderAsync(p.Id);
        var assignedToUserId = string.IsNullOrWhiteSpace(InputActivity.AssignedToUserId) ? null : InputActivity.AssignedToUserId.Trim();
        var assignedToName = string.IsNullOrWhiteSpace(assignedToUserId)
            ? (string.IsNullOrWhiteSpace(InputActivity.AssignedTo)
                ? (p.AssignedEmployee?.FullName ?? p.AssignedUserId)
                : InputActivity.AssignedTo.Trim())
            : (await _db.EmployeeProfiles.AsNoTracking()
                .Where(e => e.UserId == assignedToUserId)
                .Select(e => e.FullName)
                .FirstOrDefaultAsync() ?? assignedToUserId);

        await InsertProjectActivitySafeAsync(
            projectId: p.Id,
            assignedToName: assignedToName,
            assignedToUserId: assignedToUserId,
            description: InputActivity.Description.Trim(),
            durationValue: Math.Max(1, InputActivity.DurationValue),
            durationUnit: NormalizeDurationUnit(InputActivity.DurationUnit),
            plannedHours: ParseDurationToHours(InputActivity.DurationValue, InputActivity.DurationUnit),
            startAtUtc: InputActivity.StartDateLocal.HasValue ? DateTime.SpecifyKind(InputActivity.StartDateLocal.Value, DateTimeKind.Local).ToUniversalTime() : null,
            endAtUtc: InputActivity.EndDateLocal.HasValue ? DateTime.SpecifyKind(InputActivity.EndDateLocal.Value, DateTimeKind.Local).ToUniversalTime() : null,
            sortOrder: nextOrder
        );

        return RedirectToPage(new { id = InputActivity.ProjectId });
    }

    public async Task<IActionResult> OnPostDeleteActivityAsync(Guid id, Guid activityId)
    {
        if (!AppRoles.IsGlobalAdmin(User))
            return Forbid();

        await DeleteProjectActivitySafeAsync(id, activityId);

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostToggleActivityDoneAsync(Guid id, Guid activityId, bool done)
    {
        if (!AppRoles.IsGlobalAdmin(User))
            return Forbid();

        await EnsureProjectActivityColumnsAsync();
        var affected = await UpdateProjectActivityDoneSafeAsync(id, activityId, done);
        if (affected <= 0)
            Error = "No se pudo actualizar la actividad. Recarga la página e intenta nuevamente.";
        else
            Message = done ? "Actividad marcada como realizada." : "Actividad marcada como pendiente.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        await EnsureProjectActivityColumnsAsync();

        var isAdmin = AppRoles.IsGlobalAdmin(User);
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
        var projectActivities = await LoadProjectActivitiesSafeAsync(p.Id);

        var totalHours = Math.Max(1, (int)Math.Ceiling((p.EstimatedEndDate - p.StartDate).TotalHours));
        var elapsedHours = Math.Max(0, Math.Min(totalHours, (int)Math.Ceiling((DateTime.UtcNow - p.StartDate).TotalHours)));
        var percent = Math.Round((elapsedHours * 100d) / totalHours, 1);
        var totalPlanHours = Math.Max(1, projectActivities.Sum(a => Math.Max(1, a.PlannedDays)));
        var planRows = BuildPdfGanttRows(p.StartDate.ToLocalTime(), projectActivities, totalPlanHours, DateTime.UtcNow.ToLocalTime());
        const float timelineWidth = 160f;

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
                                header.ConstantItem(50).Text("Inicio").SemiBold().FontSize(9);
                                header.ConstantItem(50).Text("Fin").SemiBold().FontSize(9);
                                header.ConstantItem(44).Text("Duracion").SemiBold().FontSize(9);
                                header.ConstantItem(40).AlignCenter().Text("Avance").SemiBold().FontSize(9);
                                header.ConstantItem(120).Text("Tarea").SemiBold().FontSize(9);
                                header.ConstantItem(timelineWidth).Text($"Timeline ({totalPlanHours} h)").SemiBold().FontSize(9);
                            });

                            cc.Item().PaddingTop(3).LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten2);
                            cc.Item().PaddingTop(3).Row(axis =>
                            {
                                axis.ConstantItem(50 + 50 + 44 + 40 + 120).Text("");
                                axis.ConstantItem(timelineWidth).Text($"0h  ...  {Math.Max(1, totalPlanHours / 2)}h  ...  {totalPlanHours}h")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            });

                            foreach (var row in planRows)
                            {
                                cc.Item().PaddingTop(2).Row(r =>
                                {
                                    r.ConstantItem(50).Text(row.StartDate.ToString("dd/MM")).FontSize(8.5f);
                                    r.ConstantItem(50).Text(row.EndDate.ToString("dd/MM")).FontSize(8.5f);
                                    r.ConstantItem(44).Text(row.DurationLabel).FontSize(8.5f);
                                    r.ConstantItem(40).AlignCenter().Text($"{Math.Round(row.ProgressPercent)}%").FontSize(8.5f);
                                    r.ConstantItem(120).Column(task =>
                                    {
                                        task.Item().Text(row.Task).FontSize(8.5f);
                                        task.Item().Text(row.AssignedTo).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                                        if (row.IsCompleted)
                                            task.Item().Text($"Terminado: {row.CompletedText}").FontSize(7).FontColor(Colors.Green.Darken2);
                                    });

                                    r.ConstantItem(timelineWidth).Height(16).Border(0.6f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Layers(l =>
                                    {
                                        l.Layer().Background(Colors.Grey.Lighten5);
                                        var rawOffset = (float)(timelineWidth * row.OffsetPercent / 100d);
                                        var safeOffset = Math.Clamp(rawOffset, 0f, timelineWidth - 2f);
                                        var rawWidth = (float)(timelineWidth * row.WidthPercent / 100d);
                                        var safeWidth = Math.Clamp(rawWidth, 2f, Math.Max(2f, timelineWidth - safeOffset));
                                        l.PrimaryLayer().Element(bar =>
                                        {
                                            bar.PaddingLeft(safeOffset)
                                                .Width(safeWidth)
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

                        }
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Resumen técnico").SemiBold();
                        cc.Item().PaddingTop(6).Column(s =>
                        {
                            s.Spacing(5);
                            s.Item().Text(t => { t.Span("Objetivo: ").SemiBold(); t.Span(Safe(p.Objective)); });
                            s.Item().Text(t => { t.Span("Alcance: ").SemiBold(); t.Span(Safe(p.Scope)); });
                            s.Item().Text(t => { t.Span("Descripción: ").SemiBold(); t.Span(Safe(p.ActivityDescription)); });
                            s.Item().Text(t => { t.Span("Comentarios: ").SemiBold(); t.Span(Safe(p.AdditionalComments)); });
                        });
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

    private sealed record ProjectActivitySchema(
        bool HasAssignedToUserId,
        bool HasDurationUnit,
        bool HasDurationValue,
        bool HasStartAtUtc,
        bool HasEndAtUtc,
        bool HasColorHex,
        bool HasIsCompleted,
        bool HasCompletedAtUtc);

    private async Task EnsureProjectActivityColumnsAsync()
    {
        const string sql = """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='ProjectActivities' AND column_name='assignedtouserid'
                ) THEN
                    EXECUTE 'ALTER TABLE public."ProjectActivities" RENAME COLUMN assignedtouserid TO "AssignedToUserId"';
                END IF;
            END $$;
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='projectactivities' AND column_name='assignedtouserid'
                ) THEN
                    EXECUTE 'ALTER TABLE public.projectactivities RENAME COLUMN assignedtouserid TO "AssignedToUserId"';
                END IF;
            END $$;
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "AssignedToUserId" character varying(64);
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "DurationUnit" character varying(16);
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "DurationValue" integer;
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "StartAtUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "EndAtUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "ColorHex" character varying(16);
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "IsCompleted" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE IF EXISTS public."ProjectActivities" ADD COLUMN IF NOT EXISTS "CompletedAtUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "AssignedToUserId" character varying(64);
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "DurationUnit" character varying(16);
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "DurationValue" integer;
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "StartAtUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "EndAtUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "ColorHex" character varying(16);
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "IsCompleted" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE IF EXISTS public.projectactivities ADD COLUMN IF NOT EXISTS "CompletedAtUtc" timestamp with time zone;
            """;
        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (PostgresException ex) when (ex.SqlState == "42501")
        {
            // Sin permisos de owner en algunos despliegues: seguimos en modo compatibilidad.
        }
        catch
        {
            // No bloquear la pantalla por esquema parcial.
        }
    }

    private async Task<ProjectActivitySchema> GetProjectActivitySchemaAsync()
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose)
            await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema='public'
                  AND table_name IN ('ProjectActivities', 'projectactivities');
                """;
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                if (!rd.IsDBNull(0))
                    cols.Add(rd.GetString(0));
            }
        }
        finally
        {
            if (mustClose)
                await conn.CloseAsync();
        }

        return new ProjectActivitySchema(
            HasAssignedToUserId: cols.Contains("AssignedToUserId"),
            HasDurationUnit: cols.Contains("DurationUnit"),
            HasDurationValue: cols.Contains("DurationValue"),
            HasStartAtUtc: cols.Contains("StartAtUtc"),
            HasEndAtUtc: cols.Contains("EndAtUtc"),
            HasColorHex: cols.Contains("ColorHex"),
            HasIsCompleted: cols.Contains("IsCompleted"),
            HasCompletedAtUtc: cols.Contains("CompletedAtUtc"));
    }

    private async Task<List<ProjectActivity>> LoadProjectActivitiesSafeAsync(Guid projectId)
    {
        var schema = await GetProjectActivitySchemaAsync();
        var result = new List<ProjectActivity>();

        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose)
            await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT
                    p."Id",
                    p."ProjectId",
                    COALESCE(p."AssignedToName",'') AS "AssignedToName",
                    {(schema.HasAssignedToUserId ? "p.\"AssignedToUserId\"" : "NULL::character varying(64)")} AS "AssignedToUserId",
                    COALESCE(p."Description",'') AS "Description",
                    COALESCE(p."PlannedDays",1) AS "PlannedDays",
                    {(schema.HasDurationUnit ? "COALESCE(p.\"DurationUnit\", 'hours')" : "'hours'::character varying(16)")} AS "DurationUnit",
                    {(schema.HasDurationValue ? "COALESCE(p.\"DurationValue\", GREATEST(COALESCE(p.\"PlannedDays\",1),1))" : "GREATEST(COALESCE(p.\"PlannedDays\",1),1)")} AS "DurationValue",
                    {(schema.HasStartAtUtc ? "p.\"StartAtUtc\"" : "NULL::timestamp with time zone")} AS "StartAtUtc",
                    {(schema.HasEndAtUtc ? "p.\"EndAtUtc\"" : "NULL::timestamp with time zone")} AS "EndAtUtc",
                    {(schema.HasColorHex ? "p.\"ColorHex\"" : "NULL::character varying(16)")} AS "ColorHex",
                    {(schema.HasIsCompleted ? "COALESCE(p.\"IsCompleted\", FALSE)" : "FALSE")} AS "IsCompleted",
                    {(schema.HasCompletedAtUtc ? "p.\"CompletedAtUtc\"" : "NULL::timestamp with time zone")} AS "CompletedAtUtc",
                    COALESCE(p."SortOrder",0) AS "SortOrder",
                    COALESCE(p."CreatedAt", NOW()) AS "CreatedAt"
                FROM public."ProjectActivities" p
                WHERE p."ProjectId" = @pid
                ORDER BY COALESCE(p."SortOrder",0), COALESCE(p."CreatedAt", NOW());
                """;
            var pProjectId = cmd.CreateParameter();
            pProjectId.ParameterName = "pid";
            pProjectId.Value = projectId;
            cmd.Parameters.Add(pProjectId);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var rawDescription = rd.IsDBNull(4) ? "" : rd.GetString(4);
                var fallbackDone = !schema.HasIsCompleted && rawDescription.StartsWith("[DONE] ", StringComparison.OrdinalIgnoreCase);
                var normalizedDescription = fallbackDone ? rawDescription[7..].Trim() : rawDescription;
                result.Add(new ProjectActivity
                {
                    Id = rd.GetGuid(0),
                    ProjectId = rd.GetGuid(1),
                    AssignedToName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    AssignedToUserId = rd.IsDBNull(3) ? null : rd.GetString(3),
                    Description = normalizedDescription,
                    PlannedDays = rd.IsDBNull(5) ? 1 : rd.GetInt32(5),
                    DurationUnit = rd.IsDBNull(6) ? "hours" : rd.GetString(6),
                    DurationValue = rd.IsDBNull(7) ? Math.Max(1, rd.IsDBNull(5) ? 1 : rd.GetInt32(5)) : Math.Max(1, rd.GetInt32(7)),
                    StartAtUtc = rd.IsDBNull(8) ? null : rd.GetDateTime(8),
                    EndAtUtc = rd.IsDBNull(9) ? null : rd.GetDateTime(9),
                    ColorHex = rd.IsDBNull(10) ? null : rd.GetString(10),
                    IsCompleted = schema.HasIsCompleted ? (!rd.IsDBNull(11) && rd.GetBoolean(11)) : fallbackDone,
                    CompletedAtUtc = schema.HasCompletedAtUtc ? (rd.IsDBNull(12) ? null : rd.GetDateTime(12)) : null,
                    SortOrder = rd.IsDBNull(13) ? 0 : rd.GetInt32(13),
                    CreatedAt = rd.IsDBNull(14) ? DateTime.UtcNow : rd.GetDateTime(14)
                });
            }
        }
        finally
        {
            if (mustClose)
                await conn.CloseAsync();
        }

        return result;
    }

    private async Task<int> GetNextActivitySortOrderAsync(Guid projectId)
    {
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose)
            await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COALESCE(MAX("SortOrder"), 0)
                FROM public."ProjectActivities"
                WHERE "ProjectId" = @pid;
                """;
            var pProjectId = cmd.CreateParameter();
            pProjectId.ParameterName = "pid";
            pProjectId.Value = projectId;
            cmd.Parameters.Add(pProjectId);
            var scalar = await cmd.ExecuteScalarAsync();
            var max = scalar == null || scalar == DBNull.Value ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            return max + 1;
        }
        finally
        {
            if (mustClose)
                await conn.CloseAsync();
        }
    }

    private async Task InsertProjectActivitySafeAsync(
        Guid projectId,
        string assignedToName,
        string? assignedToUserId,
        string description,
        int durationValue,
        string durationUnit,
        int plannedHours,
        DateTime? startAtUtc,
        DateTime? endAtUtc,
        int sortOrder)
    {
        var schema = await GetProjectActivitySchemaAsync();
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose)
            await conn.OpenAsync();

        try
        {
            var columns = new List<string> { "\"Id\"", "\"ProjectId\"", "\"AssignedToName\"", "\"Description\"", "\"PlannedDays\"", "\"SortOrder\"", "\"CreatedAt\"" };
            var values = new List<string> { "@id", "@projectId", "@assignedToName", "@description", "@plannedDays", "@sortOrder", "@createdAt" };

            if (schema.HasAssignedToUserId) { columns.Add("\"AssignedToUserId\""); values.Add("@assignedToUserId"); }
            if (schema.HasDurationUnit) { columns.Add("\"DurationUnit\""); values.Add("@durationUnit"); }
            if (schema.HasDurationValue) { columns.Add("\"DurationValue\""); values.Add("@durationValue"); }
            if (schema.HasStartAtUtc) { columns.Add("\"StartAtUtc\""); values.Add("@startAtUtc"); }
            if (schema.HasEndAtUtc) { columns.Add("\"EndAtUtc\""); values.Add("@endAtUtc"); }
            if (schema.HasColorHex) { columns.Add("\"ColorHex\""); values.Add("@colorHex"); }
            if (schema.HasIsCompleted) { columns.Add("\"IsCompleted\""); values.Add("@isCompleted"); }
            if (schema.HasCompletedAtUtc) { columns.Add("\"CompletedAtUtc\""); values.Add("@completedAtUtc"); }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO public."ProjectActivities" ({string.Join(", ", columns)})
                VALUES ({string.Join(", ", values)});
                """;

            AddParam(cmd, "id", Guid.NewGuid());
            AddParam(cmd, "projectId", projectId);
            AddParam(cmd, "assignedToName", assignedToName ?? "");
            AddParam(cmd, "description", description ?? "");
            AddParam(cmd, "plannedDays", Math.Max(1, plannedHours));
            AddParam(cmd, "sortOrder", sortOrder);
            AddParam(cmd, "createdAt", DateTime.UtcNow);
            if (schema.HasAssignedToUserId) AddParam(cmd, "assignedToUserId", (object?)assignedToUserId ?? DBNull.Value);
            if (schema.HasDurationUnit) AddParam(cmd, "durationUnit", durationUnit);
            if (schema.HasDurationValue) AddParam(cmd, "durationValue", Math.Max(1, durationValue));
            if (schema.HasStartAtUtc) AddParam(cmd, "startAtUtc", (object?)startAtUtc ?? DBNull.Value);
            if (schema.HasEndAtUtc) AddParam(cmd, "endAtUtc", (object?)endAtUtc ?? DBNull.Value);
            if (schema.HasColorHex) AddParam(cmd, "colorHex", DBNull.Value);
            if (schema.HasIsCompleted) AddParam(cmd, "isCompleted", false);
            if (schema.HasCompletedAtUtc) AddParam(cmd, "completedAtUtc", DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (mustClose)
                await conn.CloseAsync();
        }
    }

    private async Task DeleteProjectActivitySafeAsync(Guid projectId, Guid activityId)
    {
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose)
            await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM public."ProjectActivities"
                WHERE "Id" = @activityId AND "ProjectId" = @projectId;
                """;
            AddParam(cmd, "activityId", activityId);
            AddParam(cmd, "projectId", projectId);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (mustClose)
                await conn.CloseAsync();
        }
    }

    private async Task<int> UpdateProjectActivityDoneSafeAsync(Guid projectId, Guid activityId, bool done)
    {
        var schema = await GetProjectActivitySchemaAsync();
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose)
            await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = !schema.HasIsCompleted
                ? """
                UPDATE public."ProjectActivities"
                SET "Description" = CASE
                    WHEN @done THEN CONCAT('[DONE] ', regexp_replace(COALESCE("Description", ''), '^\[DONE\]\s*', ''))
                    ELSE regexp_replace(COALESCE("Description", ''), '^\[DONE\]\s*', '')
                END
                WHERE "Id" = @activityId AND "ProjectId" = @projectId;
                """
                : schema.HasCompletedAtUtc
                ? """
                UPDATE public."ProjectActivities"
                SET "IsCompleted" = @done,
                    "CompletedAtUtc" = CASE WHEN @done THEN @nowUtc ELSE NULL END
                WHERE "Id" = @activityId AND "ProjectId" = @projectId;
                """
                : """
                UPDATE public."ProjectActivities"
                SET "IsCompleted" = @done
                WHERE "Id" = @activityId AND "ProjectId" = @projectId;
                """;
            AddParam(cmd, "done", done);
            AddParam(cmd, "nowUtc", DateTime.UtcNow);
            AddParam(cmd, "activityId", activityId);
            AddParam(cmd, "projectId", projectId);
            return await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (mustClose)
                await conn.CloseAsync();
        }
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

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

        var totalHours = Math.Max(1, acts.Sum(a => Math.Max(1, a.PlannedDays)));
        var cursor = 0;
        Activities = new List<ActivityRow>();
        var colorMap = BuildPersonColorMap(acts);

        foreach (var a in acts)
        {
            var hours = Math.Max(1, a.PlannedDays);
            var start = cursor + 1;
            var end = cursor + hours;
            var offset = (cursor * 100d) / totalHours;
            var width = (hours * 100d) / totalHours;
            cursor += hours;
            var assigned = string.IsNullOrWhiteSpace(a.AssignedToName) ? "-" : a.AssignedToName;
            var color = colorMap.TryGetValue(assigned, out var c) ? c : "#06B6D4";

            Activities.Add(new ActivityRow(
                a.Id,
                assigned,
                string.IsNullOrWhiteSpace(a.Description) ? "-" : a.Description,
                hours,
                FormatDuration(hours),
                start,
                end,
                a.StartAtUtc.HasValue ? a.StartAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-",
                a.EndAtUtc.HasValue ? a.EndAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-",
                Math.Round(width, 2),
                Math.Round(offset, 2),
                color,
                a.IsCompleted,
                a.CompletedAtUtc.HasValue ? a.CompletedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-"
            ));
        }
    }

    private static List<PdfGanttRow> BuildPdfGanttRows(DateTime planStartLocal, List<ProjectActivity> activities, int totalPlanHours, DateTime nowLocal)
    {
        var rows = new List<PdfGanttRow>();
        if (activities.Count == 0)
            return rows;

        var cursor = 0;
        var colorIndex = 0;

        foreach (var a in activities)
        {
            var hours = Math.Max(1, a.PlannedDays);
            var startHour = cursor;
            var endHour = cursor + hours;
            var startDate = planStartLocal.AddHours(startHour);
            var endDate = planStartLocal.AddHours(endHour);
            var offset = (cursor * 100d) / totalPlanHours;
            var width = (hours * 100d) / totalPlanHours;
            width = Math.Min(width, Math.Max(0, 100 - offset));
            cursor += hours;

            var finalStart = a.StartAtUtc.HasValue ? a.StartAtUtc.Value.ToLocalTime() : startDate;
            var finalEnd = a.EndAtUtc.HasValue ? a.EndAtUtc.Value.ToLocalTime() : endDate;
            var progress = a.IsCompleted ? 100d : CalcProgress(nowLocal, finalStart, finalEnd);
            rows.Add(new PdfGanttRow(
                string.IsNullOrWhiteSpace(a.Description) ? "-" : a.Description.Trim(),
                string.IsNullOrWhiteSpace(a.AssignedToName) ? "-" : a.AssignedToName.Trim(),
                finalStart,
                finalEnd,
                hours,
                FormatDuration(hours),
                progress,
                Math.Round(offset, 2),
                Math.Round(width, 2),
                colorIndex++,
                a.IsCompleted,
                a.CompletedAtUtc.HasValue ? a.CompletedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-"));
        }

        return rows;
    }

    private static double CalcProgress(DateTime nowDateTime, DateTime startDate, DateTime endDate)
    {
        if (nowDateTime < startDate)
            return 0;
        if (nowDateTime >= endDate)
            return 100;

        var total = Math.Max(1, (endDate - startDate).TotalHours);
        var elapsed = Math.Max(0, (nowDateTime - startDate).TotalHours);
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

    private static int ParseDurationToHours(int value, string? unit)
    {
        var safe = Math.Max(1, value);
        return string.Equals(unit, "days", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(1, safe * 24)
            : safe;
    }

    private static string NormalizeDurationUnit(string? unit)
    {
        return string.Equals(unit, "days", StringComparison.OrdinalIgnoreCase)
            ? "days"
            : "hours";
    }

    private static string FormatDuration(int hours)
    {
        if (hours % 24 == 0)
        {
            var days = hours / 24;
            return $"{days} d";
        }
        return $"{hours} h";
    }

    private static Dictionary<string, string> BuildPersonColorMap(IReadOnlyList<ProjectActivity> acts)
    {
        var palette = new[]
        {
            "#3B82F6", "#22C55E", "#A855F7", "#F59E0B", "#06B6D4",
            "#EF4444", "#84CC16", "#EC4899", "#0EA5E9", "#F97316"
        };

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var name in acts.Select(a => string.IsNullOrWhiteSpace(a.AssignedToName) ? "-" : a.AssignedToName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result[name] = palette[index % palette.Length];
            index++;
        }
        return result;
    }

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
