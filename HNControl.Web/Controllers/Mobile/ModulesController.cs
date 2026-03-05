using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Controllers.Mobile;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/mobile/modules")]
public class ModulesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IModuleAccessService _moduleAccess;

    public ModulesController(ApplicationDbContext db, IModuleAccessService moduleAccess)
    {
        _db = db;
        _moduleAccess = moduleAccess;
    }

    public record ModuleItemDto(string Key, string Label);
    public record MonitorItemDto(Guid Id, string Client, string Name, string ProbeType, string Address, string Status, DateTime? LastCheckedAt, int? LastLatencyMs, string LastError);
    public record InventoryOrderDto(Guid AnchorId, DateTime RequestedAt, string Type, string ProjectTitle, string ResponsibleName, string StatusLabel, int LinesCount, string ItemsPreview);
    public record CarrierClientDto(Guid ClientId, string Name, int ServicesCount, string CarriersSummary);
    public record ProjectItemDto(Guid Id, string Client, string Title, string Status, DateTime StartDate, DateTime EstimatedEndDate);
    public record KnowledgeItemDto(Guid Id, string Title, string Category, string DocType, string Status, DateTime UpdatedAt, string Url);
    public record LeaveItemDto(Guid Id, string Type, string Status, DateTime StartDate, DateTime EndDate, int TotalDays, DateTime RequestedAt);
    public record ExamItemDto(Guid AssignmentId, string Title, string Status, DateTime AssignedAt, DateTime? DueAt, decimal Score, decimal MaxScore);
    public record Eval360ItemDto(Guid AssignmentId, string Campaign, string Role, string Status, DateTime CreatedAt, DateTime? SubmittedAt);

    [HttpGet]
    [ProducesResponseType(typeof(List<ModuleItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListModules()
    {
        var set = await _moduleAccess.GetAllowedModulesAsync(User);
        var mobileEmployeeModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppModules.Monitoring,
            AppModules.Inventory,
            AppModules.Carriers,
            AppModules.Viaticos,
            AppModules.Projects,
            AppModules.Knowledge,
            AppModules.Leaves,
            AppModules.Exams,
            AppModules.Eval360
        };

        var data = AppModules.All
            .Where(x => mobileEmployeeModules.Contains(x.Key) && set.Contains(x.Key))
            .Select(x => new ModuleItemDto(x.Key, x.Label))
            .ToList();
        return Ok(data);
    }

    [HttpGet("monitoring")]
    [ProducesResponseType(typeof(List<MonitorItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Monitoring()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Monitoring))
            return Forbid();

        var rows = await _db.MonitorTargets
            .AsNoTracking()
            .Include(t => t.Client)
            .OrderBy(t => t.Client!.Name)
            .ThenBy(t => t.Name)
            .Take(300)
            .Select(t => new MonitorItemDto(
                t.Id,
                t.Client != null ? t.Client.Name : "-",
                t.Name,
                t.ProbeType.ToString(),
                !string.IsNullOrWhiteSpace(t.IpAddress) ? t.IpAddress : t.Fqdn,
                t.LastStatus.ToString(),
                t.LastCheckedAt,
                t.LastLatencyMs,
                t.LastError))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("inventory/my-requests")]
    [ProducesResponseType(typeof(List<InventoryOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InventoryMyRequests()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Inventory))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var lines = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Where(m => m.RequestedByUserId == userId || m.ResponsibleUserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .Take(2000)
            .ToListAsync();

        string LineLabel(InventoryMovement m)
        {
            var name = m.Item?.Name ?? "-";
            var unit = m.Item?.Unit ?? "";
            return $"{name} ({m.Quantity} {unit})";
        }

        string StatusBadge(IEnumerable<InventoryMovement> g)
        {
            var statuses = g.Select(x => x.Status).Distinct().ToList();
            if (statuses.Count == 1)
            {
                return statuses[0] switch
                {
                    InventoryMovementStatus.Pending => "Pendiente",
                    InventoryMovementStatus.Approved => "Aprobado",
                    InventoryMovementStatus.Rejected => "Rechazado",
                    _ => "-"
                };
            }
            return g.Any(x => x.Status == InventoryMovementStatus.Pending) ? "Parcial pendiente" : "Parcial";
        }

        var orders = lines
            .GroupBy(m => new { m.RequestedAt, m.RequestedByUserId, m.Type, m.ProjectId, m.ResponsibleUserId })
            .OrderByDescending(g => g.Key.RequestedAt)
            .Take(300)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.Id).First();
                var previewList = g.OrderByDescending(x => x.Quantity).Take(3).Select(LineLabel).ToList();
                var preview = string.Join(", ", previewList);
                if (g.Count() > 3) preview += $" y {g.Count() - 3} mas";

                return new InventoryOrderDto(
                    first.Id,
                    g.Key.RequestedAt,
                    g.Key.Type.ToString(),
                    first.Project?.Title ?? "-",
                    string.IsNullOrWhiteSpace(first.ResponsibleName) ? "-" : first.ResponsibleName,
                    StatusBadge(g),
                    g.Count(),
                    string.IsNullOrWhiteSpace(preview) ? "-" : preview);
            })
            .ToList();

        return Ok(orders);
    }

    [HttpGet("carriers")]
    [ProducesResponseType(typeof(List<CarrierClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Carriers()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Carriers))
            return Forbid();

        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var ids = clients.Select(c => c.Id).ToList();

        var services = await _db.ClientCarrierServices
            .AsNoTracking()
            .Include(s => s.Carrier)
            .Where(s => ids.Contains(s.ClientId) && s.IsActive)
            .ToListAsync();

        var grouped = services
            .GroupBy(s => s.ClientId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Count = g.Count(),
                    Carriers = string.Join(", ", g.Select(x => x.Carrier != null ? x.Carrier.Name : "(Sin carrier)")
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3))
                });

        var data = clients.Select(c =>
        {
            grouped.TryGetValue(c.Id, out var g);
            return new CarrierClientDto(c.Id, c.Name, g?.Count ?? 0, g?.Carriers ?? "");
        }).ToList();

        return Ok(data);
    }

    [HttpGet("projects")]
    [ProducesResponseType(typeof(List<ProjectItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Projects()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Projects))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var rows = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Client)
            .Where(p => p.AssignedUserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(200)
            .Select(p => new ProjectItemDto(
                p.Id,
                p.Client != null ? p.Client.Name : "-",
                p.Title,
                p.Status == ProjectStatus.Closed ? "Cerrado" : "Abierto",
                p.StartDate,
                p.EstimatedEndDate))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("knowledge")]
    [ProducesResponseType(typeof(List<KnowledgeItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Knowledge()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Knowledge))
            return Forbid();

        var rows = await _db.KnowledgeLinks
            .AsNoTracking()
            .Where(k => k.Status == KnowledgeStatus.Publicado)
            .OrderByDescending(k => k.IsPinned)
            .ThenByDescending(k => k.UpdatedAt)
            .Take(300)
            .Select(k => new KnowledgeItemDto(
                k.Id,
                k.Title,
                k.Category,
                k.DocType.ToString(),
                k.Status.ToString(),
                k.UpdatedAt,
                k.Url))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("leaves")]
    [ProducesResponseType(typeof(List<LeaveItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Leaves()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Leaves))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var rows = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RequestedAt)
            .Take(200)
            .Select(x => new LeaveItemDto(
                x.Id,
                x.Type.ToString(),
                x.Status.ToString(),
                x.StartDate,
                x.EndDate,
                x.TotalDays,
                x.RequestedAt))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("exams")]
    [ProducesResponseType(typeof(List<ExamItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Exams()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Exams))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var rows = await _db.ExamAssignments
            .AsNoTracking()
            .Include(x => x.Exam)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AssignedAt)
            .Take(200)
            .Select(x => new ExamItemDto(
                x.Id,
                x.Exam != null ? x.Exam.Title : "-",
                x.Status.ToString(),
                x.AssignedAt,
                x.DueAt,
                x.Score,
                x.MaxScore))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("eval360")]
    [ProducesResponseType(typeof(List<Eval360ItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Eval360()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Eval360))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var rows = await _db.Eval360Assignments
            .AsNoTracking()
            .Include(x => x.Campaign)
            .Where(x => x.EvaluatorUserId == userId || x.SubjectUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new Eval360ItemDto(
                x.Id,
                x.Campaign != null ? x.Campaign.Title : "-",
                x.EvaluatorUserId == userId ? "Evaluador" : "Evaluado",
                x.Status == Eval360AssignmentStatus.Submitted ? "Enviado" : "Pendiente",
                x.CreatedAt,
                x.SubmittedAt))
            .ToListAsync();

        return Ok(rows);
    }
}
