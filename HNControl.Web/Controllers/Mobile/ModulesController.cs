using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Tickets;
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
    private readonly ITicketFlowService _ticketFlow;
    private readonly IFileStorage _storage;

    public ModulesController(ApplicationDbContext db, IModuleAccessService moduleAccess, ITicketFlowService ticketFlow, IFileStorage storage)
    {
        _db = db;
        _moduleAccess = moduleAccess;
        _ticketFlow = ticketFlow;
        _storage = storage;
    }

    public record ModuleItemDto(string Key, string Label);
    public record ApiMessageDto(string Message);
    public record MonitorItemDto(Guid Id, string Client, string Name, string ProbeType, string Address, string Status, DateTime? LastCheckedAt, int? LastLatencyMs, string LastError);
    public record InventoryOrderDto(Guid AnchorId, DateTime RequestedAt, string Type, string ProjectTitle, string ResponsibleName, string StatusLabel, int LinesCount, string ItemsPreview);
    public record InventoryCatalogItemDto(Guid Id, string Name, string Sku, string Category, string Location, string Unit, decimal Stock);
    public record InventoryProjectDto(Guid Id, string Title);
    public record InventoryCatalogDto(List<InventoryCatalogItemDto> Items, List<InventoryProjectDto> Projects);
    public record InventoryRequestLineDto(Guid ItemId, decimal Quantity, Guid? AssignedClientId, string SerialNumber, string Reference, string Notes);
    public record InventoryCreateRequestDto(string Type, Guid? ProjectId, string Notes, List<InventoryRequestLineDto> Lines);
    public record CarrierClientDto(Guid ClientId, string Name, int ServicesCount, string CarriersSummary);
    public record ProjectItemDto(Guid Id, string Client, string Title, string Status, DateTime StartDate, DateTime EstimatedEndDate);
    public record KnowledgeItemDto(Guid Id, string Title, string Category, string DocType, string Status, DateTime UpdatedAt, string Url);
    public record LeaveItemDto(Guid Id, string Type, string Status, DateTime StartDate, DateTime EndDate, int TotalDays, DateTime RequestedAt);
    public record LeaveDetailDto(Guid Id, string Type, string Status, DateTime StartDate, DateTime EndDate, int TotalDays, DateTime RequestedAt, DateTime? ReviewedAt, string Reason, string AdminComment, List<string> EvidenceFiles);
    public record ExamItemDto(Guid AssignmentId, string Title, string Status, DateTime AssignedAt, DateTime? DueAt, decimal Score, decimal MaxScore);
    public record ExamTakeChoiceDto(Guid ChoiceId, int Ordinal, string Text);
    public record ExamTakeQuestionDto(Guid QuestionId, int Ordinal, string Type, string Text, decimal Points, bool IsRequired, string TextAnswer, List<Guid> SelectedChoiceIds, List<ExamTakeChoiceDto> Choices);
    public record ExamTakeDto(Guid AssignmentId, string Title, string Description, string Status, DateTime? DueAt, decimal Score, decimal MaxScore, List<ExamTakeQuestionDto> Questions);
    public record ExamTakeAnswerInputDto(Guid QuestionId, string? TextAnswer, List<Guid>? ChoiceIds);
    public record ExamTakeSaveDto(List<ExamTakeAnswerInputDto> Answers);
    public record Eval360ItemDto(Guid AssignmentId, string Campaign, string Role, string Status, DateTime CreatedAt, DateTime? SubmittedAt);
    public record Eval360TakeQuestionDto(Guid QuestionId, string Text, int Score);
    public record Eval360TakeCompetencyDto(Guid CompetencyId, string Competency, string Comment, List<Eval360TakeQuestionDto> Questions);
    public record Eval360TakeDto(Guid AssignmentId, string Campaign, string SubjectName, string Status, List<Eval360TakeCompetencyDto> Competencies);
    public record Eval360SubmitScoreDto(Guid QuestionId, int Score);
    public record Eval360SubmitCommentDto(Guid CompetencyId, string Comment);
    public record Eval360SubmitDto(List<Eval360SubmitScoreDto> Scores, List<Eval360SubmitCommentDto> Comments);
    public record CarrierServiceDto(Guid Id, string Carrier, string ServiceLabel, string Plan, string AccountNumber, string ContractNumber, string CircuitId, string ServiceAddress, string IpInfo, string SupportPhone, string Notes, string LastNotesSummary);
    public record CarrierClientDetailDto(Guid ClientId, string ClientName, string Rfc, string Email, string Phone, List<CarrierServiceDto> Services);
    public record MonitorCheckDto(DateTime CheckedAt, bool Success, int? LatencyMs, string Error);
    public record MonitorDetailDto(Guid Id, string Client, string Name, string ProbeType, string Address, string Status, DateTime? LastCheckedAt, int? LastLatencyMs, string LastError, string ContractLabel, string CarrierServiceLabel, string Notes, List<MonitorCheckDto> LastChecks);
    public record TicketItemDto(Guid Id, string TicketNumber, string Client, string Title, string Status, string Priority, string Source, string AssignedTo, DateTime CreatedAt, DateTime SlaResponseDueAt, DateTime SlaResolutionDueAt, bool Breach, bool IsMine, bool CanTake);
    public record TicketDetailDto(
        Guid Id,
        string TicketNumber,
        string Client,
        string Contract,
        string Branch,
        string BranchAddress,
        string Carrier,
        string CarrierService,
        string CarrierAccount,
        string CarrierCircuit,
        string CarrierIp,
        string Title,
        string Description,
        string Status,
        string Priority,
        string Source,
        string AssignedTo,
        DateTime CreatedAt,
        DateTime SlaResponseDueAt,
        DateTime SlaResolutionDueAt,
        bool Breach,
        string ResolutionSummary,
        List<TicketEventDto> Events,
        List<TicketAttachmentDto> Attachments);
    public record TicketEventDto(DateTime CreatedAt, string EventType, string UserName, string Message);
    public record TicketAttachmentDto(Guid Id, string FileName, string ContentType, DateTime UploadedAt, string UploadedBy);

    [HttpGet]
    [ProducesResponseType(typeof(List<ModuleItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListModules()
    {
        var set = await _moduleAccess.GetAllowedModulesAsync(User);
        var mobileEmployeeModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppModules.ServiceOrders,
            AppModules.Tickets,
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

        var allowed = AppRoles.IsGlobalAdmin(User) ? set : set.Where(mobileEmployeeModules.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var data = AppModules.All
            .Where(x => allowed.Contains(x.Key))
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

    [HttpGet("monitoring/{id:guid}")]
    [ProducesResponseType(typeof(MonitorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MonitoringDetail(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Monitoring))
            return Forbid();

        var target = await _db.MonitorTargets
            .AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.ClientServiceContract)
            .Include(t => t.ClientCarrierService)
            .Include(t => t.Checks)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (target == null) return NotFound();

        var checks = target.Checks
            .OrderByDescending(c => c.CheckedAt)
            .Take(20)
            .Select(c => new MonitorCheckDto(c.CheckedAt, c.Success, c.LatencyMs, c.Error))
            .ToList();

        var detail = new MonitorDetailDto(
            target.Id,
            target.Client?.Name ?? "-",
            target.Name,
            target.ProbeType.ToString(),
            !string.IsNullOrWhiteSpace(target.IpAddress) ? target.IpAddress : target.Fqdn,
            target.LastStatus.ToString(),
            target.LastCheckedAt,
            target.LastLatencyMs,
            target.LastError,
            target.ClientServiceContract?.Label ?? "-",
            target.ClientCarrierService?.ServiceLabel ?? "-",
            target.Notes,
            checks);

        return Ok(detail);
    }

    [HttpGet("tickets")]
    [ProducesResponseType(typeof(List<TicketItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Tickets([FromQuery] string? status = null)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var st = (status ?? "open").Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        var q = _db.Tickets
            .AsNoTracking()
            .Include(t => t.Client)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        if (st == "open")
            q = q.Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        else if (st == "mine")
            q = q.Where(t => t.AssignedToUserId == uid);
        else if (st == "closed")
            q = q.Where(t => t.Status == TicketStatus.Closed);

        var data = await q.Take(200).Select(t => new TicketItemDto(
            t.Id,
            t.TicketNumber,
            t.Client != null ? t.Client.Name : "-",
            t.Title,
            t.Status.ToString(),
            t.Priority.ToString(),
            t.Source.ToString(),
            string.IsNullOrWhiteSpace(t.AssignedToName) ? "Sin asignar" : t.AssignedToName,
            t.CreatedAt,
            t.SlaResponseDueAt,
            t.SlaResolutionDueAt,
            t.SlaBreachedResponse || t.SlaBreachedResolution || (t.FirstResponseAt == null && now > t.SlaResponseDueAt) || (t.ResolvedAt == null && now > t.SlaResolutionDueAt),
            t.AssignedToUserId == uid,
            t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled
        )).ToListAsync();

        return Ok(data);
    }

    [HttpGet("tickets/{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TicketDetail(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();

        var now = DateTime.UtcNow;

        var t = await _db.Tickets
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .Include(x => x.Events)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        var carrier = new
        {
            Name = "-",
            Service = "-",
            Account = "-",
            Circuit = "-",
            Ip = "-"
        };

        if (t.ClientServiceContractId.HasValue)
        {
            var c = await _db.ClientCarrierServices
                .AsNoTracking()
                .Include(s => s.Carrier)
                .Where(s => s.ClientServiceContractId == t.ClientServiceContractId.Value)
                .OrderBy(s => s.ServiceLabel)
                .Select(s => new
                {
                    Name = s.Carrier != null ? s.Carrier.Name : "-",
                    Service = string.IsNullOrWhiteSpace(s.ServiceLabel) ? "-" : s.ServiceLabel,
                    Account = string.IsNullOrWhiteSpace(s.AccountNumber) ? "-" : s.AccountNumber,
                    Circuit = string.IsNullOrWhiteSpace(s.CircuitId) ? "-" : s.CircuitId,
                    Ip = string.IsNullOrWhiteSpace(s.IpInfo) ? "-" : s.IpInfo
                })
                .FirstOrDefaultAsync();

            if (c != null)
                carrier = c;
        }

        var dto = new TicketDetailDto(
            t.Id,
            t.TicketNumber,
            t.Client?.Name ?? "-",
            t.ClientServiceContract?.Label ?? "-",
            string.IsNullOrWhiteSpace(t.ClientServiceContract?.Branch) ? "-" : t.ClientServiceContract!.Branch,
            string.IsNullOrWhiteSpace(t.ClientServiceContract?.BranchAddress) ? "-" : t.ClientServiceContract!.BranchAddress,
            carrier.Name,
            carrier.Service,
            carrier.Account,
            carrier.Circuit,
            carrier.Ip,
            t.Title,
            t.Description,
            t.Status.ToString(),
            t.Priority.ToString(),
            t.Source.ToString(),
            string.IsNullOrWhiteSpace(t.AssignedToName) ? "Sin asignar" : t.AssignedToName,
            t.CreatedAt,
            t.SlaResponseDueAt,
            t.SlaResolutionDueAt,
            t.SlaBreachedResponse || t.SlaBreachedResolution || (t.FirstResponseAt == null && now > t.SlaResponseDueAt) || (t.ResolvedAt == null && now > t.SlaResolutionDueAt),
            t.ResolutionSummary,
            t.Events.OrderByDescending(e => e.CreatedAt).Take(60).Select(e => new TicketEventDto(e.CreatedAt, e.EventType, e.UserName, e.Message)).ToList(),
            t.Attachments.OrderByDescending(a => a.UploadedAt).Take(50).Select(a => new TicketAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.UploadedAt, a.UploadedByName)).ToList()
        );

        return Ok(dto);
    }

    [HttpPost("tickets/{id:guid}/take")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TicketTake(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        var ok = await _ticketFlow.TryTakeAsync(id, uid, uname, AppRoles.IsGlobalAdmin(User));
        return Ok(new ApiMessageDto(ok ? "Ticket tomado." : "No se pudo tomar ticket."));
    }

    [HttpPost("tickets/{id:guid}/start")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TicketStart(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        var ok = await _ticketFlow.TryStartAsync(id, uid, uname, AppRoles.IsGlobalAdmin(User));
        return Ok(new ApiMessageDto(ok ? "Ticket en proceso." : "No se pudo iniciar."));
    }

    public record TicketResolveBody(string Summary);
    public record TicketNoteBody(string Note);

    [HttpPost("tickets/{id:guid}/resolve")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TicketResolve(Guid id, [FromBody] TicketResolveBody body)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        var ok = await _ticketFlow.TryResolveAsync(id, uid, uname, body.Summary ?? "", AppRoles.IsGlobalAdmin(User));
        return Ok(new ApiMessageDto(ok ? "Ticket resuelto." : "No se pudo resolver."));
    }

    [HttpPost("tickets/{id:guid}/note")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TicketNote(Guid id, [FromBody] TicketNoteBody body)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        var ok = await _ticketFlow.AddNoteWithEvidenceAsync(id, uid, uname, body.Note ?? "", null, AppRoles.IsGlobalAdmin(User));
        return Ok(new ApiMessageDto(ok ? "Nota agregada." : "No se pudo agregar nota."));
    }

    [HttpPost("tickets/{id:guid}/evidence")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TicketEvidence(Guid id, [FromForm] string? note, [FromForm] IFormFile? file)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        var ok = await _ticketFlow.AddNoteWithEvidenceAsync(id, uid, uname, note ?? "", file, AppRoles.IsGlobalAdmin(User));
        return Ok(new ApiMessageDto(ok ? "Evidencia guardada." : "No se pudo guardar evidencia."));
    }

    [HttpGet("tickets/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TicketAttachmentDownload(Guid attachmentId)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();

        var att = await _db.TicketAttachments
            .AsNoTracking()
            .Include(a => a.Ticket!)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (att == null || att.Ticket == null) return NotFound();

        var downloadName = string.IsNullOrWhiteSpace(att.OriginalFileName) ? "adjunto" : att.OriginalFileName;
        var (stream, contentType, _) = await _storage.OpenAsync(att.StoragePath, downloadName);
        return File(stream, contentType, downloadName);
    }

    [HttpPost("tickets/{id:guid}/close")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TicketClose(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Tickets))
            return Forbid();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        var ok = await _ticketFlow.TryCloseAsync(id, uid, uname, AppRoles.IsGlobalAdmin(User));
        return Ok(new ApiMessageDto(ok ? "Ticket cerrado." : "No se pudo cerrar."));
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

    [HttpGet("inventory/catalog")]
    [ProducesResponseType(typeof(InventoryCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InventoryCatalog()
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Inventory))
            return Forbid();

        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Take(600)
            .Select(i => new InventoryCatalogItemDto(
                i.Id,
                i.Name,
                i.Sku ?? "",
                i.Category ?? "",
                i.Location ?? "",
                i.Unit,
                i.QuantityOnHand))
            .ToListAsync();

        var projects = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Title)
            .Take(250)
            .Select(p => new InventoryProjectDto(p.Id, p.Title))
            .ToListAsync();

        return Ok(new InventoryCatalogDto(items, projects));
    }

    [HttpPost("inventory/request")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InventoryCreateRequest([FromBody] InventoryCreateRequestDto body)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Inventory))
            return Forbid();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(uid))
            return Unauthorized();

        var typeRaw = (body.Type ?? "").Trim();
        var type = typeRaw.Equals("in", StringComparison.OrdinalIgnoreCase)
            ? InventoryMovementType.In
            : typeRaw.Equals("out", StringComparison.OrdinalIgnoreCase)
                ? InventoryMovementType.Out
                : (InventoryMovementType?)null;
        if (!type.HasValue)
            return BadRequest(new ApiMessageDto("Tipo de movimiento invalido. Usa 'in' o 'out'."));

        var lines = (body.Lines ?? new List<InventoryRequestLineDto>())
            .Where(x => x.ItemId != Guid.Empty && x.Quantity > 0)
            .ToList();
        if (lines.Count == 0)
            return BadRequest(new ApiMessageDto("Agrega al menos una linea valida."));

        var itemIds = lines.Select(x => x.ItemId).Distinct().ToList();
        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && i.IsActive)
            .Select(i => new { i.Id, i.IsConsumable })
            .ToListAsync();
        if (items.Count != itemIds.Count)
            return BadRequest(new ApiMessageDto("Uno o mas items no existen o estan inactivos."));

        var itemMap = items.ToDictionary(x => x.Id, x => x);
        var me = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == uid);
        var meName = me?.FullName ?? (User.Identity?.Name ?? "Empleado");
        var now = DateTime.UtcNow;
        var globalNotes = (body.Notes ?? "").Trim();

        foreach (var l in lines)
        {
            var info = itemMap[l.ItemId];
            var lineNotes = (l.Notes ?? "").Trim();
            var notes = string.Join("\n", new[] { globalNotes, lineNotes }.Where(x => !string.IsNullOrWhiteSpace(x)));

            _db.InventoryMovements.Add(new InventoryMovement
            {
                ItemId = l.ItemId,
                Type = type.Value,
                Status = InventoryMovementStatus.Pending,
                Quantity = l.Quantity,
                ProjectId = body.ProjectId,
                RequestedAt = now,
                RequestedByUserId = uid,
                RequestedByName = meName,
                ResponsibleUserId = uid,
                ResponsibleName = meName,
                AssignedClientId = type.Value == InventoryMovementType.Out && !info.IsConsumable ? l.AssignedClientId : null,
                SerialNumber = type.Value == InventoryMovementType.Out && !info.IsConsumable ? (l.SerialNumber ?? "").Trim() : "",
                Reference = type.Value == InventoryMovementType.In ? (l.Reference ?? "").Trim() : "",
                Notes = notes
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new ApiMessageDto("Solicitud enviada para aprobacion."));
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

    [HttpGet("carriers/{clientId:guid}")]
    [ProducesResponseType(typeof(CarrierClientDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CarrierClientDetail(Guid clientId)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Carriers))
            return Forbid();

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
        if (client == null) return NotFound();

        var services = await _db.ClientCarrierServices
            .AsNoTracking()
            .Include(s => s.Carrier)
            .Include(s => s.CarrierNotes)
            .Where(s => s.ClientId == clientId && s.IsActive)
            .OrderBy(s => s.ServiceLabel)
            .ToListAsync();

        var rows = services.Select(s =>
        {
            var notesSummary = string.Join(" | ", s.CarrierNotes
                .OrderByDescending(n => n.CreatedAt)
                .Take(2)
                .Select(n => $"{n.NoteType}: {n.Message}"));

            return new CarrierServiceDto(
                s.Id,
                s.Carrier?.Name ?? "-",
                s.ServiceLabel,
                s.Plan,
                s.AccountNumber,
                s.ContractNumber,
                s.CircuitId,
                s.ServiceAddress,
                s.IpInfo,
                !string.IsNullOrWhiteSpace(s.SupportPhoneOverride) ? s.SupportPhoneOverride : (s.Carrier?.SupportPhone ?? "-"),
                s.Notes,
                notesSummary);
        }).ToList();

        return Ok(new CarrierClientDetailDto(
            client.Id,
            client.Name,
            client.Rfc,
            client.Email,
            client.Phone,
            rows));
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

    [HttpGet("leaves/{id:guid}")]
    [ProducesResponseType(typeof(LeaveDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveDetail(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Leaves))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var item = await _db.LeaveRequests
            .AsNoTracking()
            .Include(x => x.Evidences)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (item == null) return NotFound();

        var detail = new LeaveDetailDto(
            item.Id,
            item.Type.ToString(),
            item.Status.ToString(),
            item.StartDate,
            item.EndDate,
            item.TotalDays,
            item.RequestedAt,
            item.ReviewedAt,
            item.Reason,
            item.AdminComment,
            item.Evidences.OrderByDescending(e => e.UploadedAt).Select(e => e.OriginalFileName).ToList());

        return Ok(detail);
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

    [HttpGet("exams/{assignmentId:guid}")]
    [ProducesResponseType(typeof(ExamTakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExamTake(Guid assignmentId)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Exams))
            return Forbid();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var isAdmin = AppRoles.IsGlobalAdmin(User);

        var a = await _db.ExamAssignments
            .Include(x => x.Exam!)
                .ThenInclude(e => e.Questions)
                    .ThenInclude(q => q.Choices)
            .Include(x => x.Answers)
                .ThenInclude(ans => ans.SelectedChoices)
            .FirstOrDefaultAsync(x => x.Id == assignmentId);

        if (a == null) return NotFound();
        if (!isAdmin && a.UserId != uid) return Forbid();

        if (a.Status == ExamAssignmentStatus.Assigned)
            a.Status = ExamAssignmentStatus.InProgress;
        a.StartedAt ??= DateTime.UtcNow;
        if (a.MaxScore <= 0m) a.MaxScore = a.Exam?.Questions.Sum(q => q.Points) ?? 0m;
        await _db.SaveChangesAsync();

        var questions = (a.Exam?.Questions ?? new List<ExamQuestion>())
            .OrderBy(q => q.Ordinal)
            .Select(q =>
            {
                var ans = a.Answers.FirstOrDefault(x => x.QuestionId == q.Id);
                return new ExamTakeQuestionDto(
                    q.Id,
                    q.Ordinal,
                    q.Type.ToString(),
                    q.Text,
                    q.Points,
                    q.IsRequired,
                    ans?.TextAnswer ?? "",
                    ans?.SelectedChoices.Select(sc => sc.ChoiceId).ToList() ?? new List<Guid>(),
                    q.Choices.OrderBy(c => c.Ordinal).Select(c => new ExamTakeChoiceDto(c.Id, c.Ordinal, c.Text)).ToList()
                );
            }).ToList();

        var dto = new ExamTakeDto(
            a.Id,
            a.Exam?.Title ?? "Examen",
            a.Exam?.Description ?? "",
            a.Status.ToString(),
            a.DueAt,
            a.Score,
            a.MaxScore,
            questions);

        return Ok(dto);
    }

    [HttpPost("exams/{assignmentId:guid}/save")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExamSave(Guid assignmentId, [FromBody] ExamTakeSaveDto body)
    {
        return await SaveExamInternalAsync(assignmentId, body, submit: false);
    }

    [HttpPost("exams/{assignmentId:guid}/submit")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExamSubmit(Guid assignmentId, [FromBody] ExamTakeSaveDto body)
    {
        return await SaveExamInternalAsync(assignmentId, body, submit: true);
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

    [HttpGet("eval360/{assignmentId:guid}")]
    [ProducesResponseType(typeof(Eval360TakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eval360Take(Guid assignmentId)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Eval360))
            return Forbid();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var isAdmin = AppRoles.IsGlobalAdmin(User);

        var assignment = await _db.Eval360Assignments
            .Include(a => a.Campaign)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);

        if (assignment == null) return NotFound();
        if (!isAdmin && assignment.EvaluatorUserId != uid) return Forbid();
        if (!isAdmin && assignment.Campaign?.Status != Eval360CampaignStatus.Open) return Forbid();

        if (assignment.StartedAt == null)
        {
            assignment.StartedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var subjectName = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(e => e.UserId == assignment.SubjectUserId)
            .Select(e => e.FullName)
            .FirstOrDefaultAsync() ?? "Empleado";

        var competencies = await _db.Eval360Competencies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Questions = c.Questions
                    .Where(q => q.IsActive)
                    .OrderBy(q => q.SortOrder)
                    .Select(q => new { q.Id, q.Text })
                    .ToList()
            })
            .ToListAsync();

        var answerMap = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.AssignmentId == assignmentId)
            .ToDictionaryAsync(a => a.QuestionId, a => a.Score);

        var commentMap = await _db.Eval360Comments
            .AsNoTracking()
            .Where(c => c.AssignmentId == assignmentId)
            .ToDictionaryAsync(c => c.CompetencyId, c => c.CommentText);

        var dto = new Eval360TakeDto(
            assignment.Id,
            assignment.Campaign?.Title ?? "Evaluación 360",
            subjectName,
            assignment.Status == Eval360AssignmentStatus.Submitted ? "Enviado" : "Pendiente",
            competencies.Select(c => new Eval360TakeCompetencyDto(
                c.Id,
                c.Name,
                commentMap.TryGetValue(c.Id, out var cm) ? cm : "",
                c.Questions.Select(q => new Eval360TakeQuestionDto(
                    q.Id,
                    q.Text,
                    answerMap.TryGetValue(q.Id, out var sc) ? sc : 3)).ToList()
            )).ToList()
        );

        return Ok(dto);
    }

    [HttpPost("eval360/{assignmentId:guid}/submit")]
    [ProducesResponseType(typeof(ApiMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eval360Submit(Guid assignmentId, [FromBody] Eval360SubmitDto body)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Eval360))
            return Forbid();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var isAdmin = AppRoles.IsGlobalAdmin(User);

        var assignment = await _db.Eval360Assignments
            .Include(a => a.Campaign)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);

        if (assignment == null) return NotFound();
        if (!isAdmin && assignment.EvaluatorUserId != uid) return Forbid();
        if (!isAdmin && assignment.Campaign?.Status != Eval360CampaignStatus.Open) return Forbid();

        var validQuestionIds = (await _db.Eval360Questions
            .AsNoTracking()
            .Where(q => q.IsActive)
            .Select(q => q.Id)
            .ToListAsync())
            .ToHashSet();

        var validCompetencyIds = (await _db.Eval360Competencies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .ToListAsync())
            .ToHashSet();

        var cleanScores = (body.Scores ?? new List<Eval360SubmitScoreDto>())
            .Where(s => validQuestionIds.Contains(s.QuestionId))
            .Select(s => new Eval360SubmitScoreDto(s.QuestionId, Math.Clamp(s.Score, 1, 5)))
            .ToList();

        if (cleanScores.Count == 0)
            return BadRequest(new ApiMessageDto("No se enviaron respuestas válidas."));

        var oldAnswers = await _db.Eval360Answers.Where(a => a.AssignmentId == assignmentId).ToListAsync();
        _db.Eval360Answers.RemoveRange(oldAnswers);

        var oldComments = await _db.Eval360Comments.Where(c => c.AssignmentId == assignmentId).ToListAsync();
        _db.Eval360Comments.RemoveRange(oldComments);

        foreach (var s in cleanScores)
        {
            _db.Eval360Answers.Add(new Eval360Answer
            {
                AssignmentId = assignmentId,
                QuestionId = s.QuestionId,
                Score = s.Score,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var c in body.Comments ?? new List<Eval360SubmitCommentDto>())
        {
            if (!validCompetencyIds.Contains(c.CompetencyId)) continue;
            var txt = (c.Comment ?? "").Trim();
            if (string.IsNullOrWhiteSpace(txt)) continue;

            _db.Eval360Comments.Add(new Eval360Comment
            {
                AssignmentId = assignmentId,
                CompetencyId = c.CompetencyId,
                CommentText = txt,
                CreatedAt = DateTime.UtcNow
            });
        }

        assignment.Status = Eval360AssignmentStatus.Submitted;
        assignment.SubmittedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new ApiMessageDto("Evaluación 360 enviada."));
    }

    private async Task<IActionResult> SaveExamInternalAsync(Guid assignmentId, ExamTakeSaveDto body, bool submit)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Exams))
            return Forbid();

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var isAdmin = AppRoles.IsGlobalAdmin(User);

        var a = await _db.ExamAssignments
            .Include(x => x.Exam!)
                .ThenInclude(e => e.Questions)
                    .ThenInclude(q => q.Choices)
            .Include(x => x.Answers)
                .ThenInclude(ans => ans.SelectedChoices)
            .FirstOrDefaultAsync(x => x.Id == assignmentId);

        if (a == null) return NotFound();
        if (!isAdmin && a.UserId != uid) return Forbid();
        if (a.Status is ExamAssignmentStatus.Submitted or ExamAssignmentStatus.Graded)
            return Ok(new ApiMessageDto("Este examen ya fue enviado."));

        var questionMap = (a.Exam?.Questions ?? new List<ExamQuestion>()).ToDictionary(q => q.Id, q => q);
        var inputMap = (body.Answers ?? new List<ExamTakeAnswerInputDto>()).ToDictionary(x => x.QuestionId, x => x);

        foreach (var q in questionMap.Values)
        {
            if (!inputMap.TryGetValue(q.Id, out var input))
                continue;

            var ans = a.Answers.FirstOrDefault(x => x.QuestionId == q.Id);
            if (ans == null)
            {
                ans = new ExamAnswer
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = a.Id,
                    QuestionId = q.Id
                };
                _db.ExamAnswers.Add(ans);
                a.Answers.Add(ans);
            }

            ans.TextAnswer = (input.TextAnswer ?? "").Trim();
            ans.UpdatedAt = DateTime.UtcNow;

            if (q.Type != ExamQuestionType.OpenText && q.Type != ExamQuestionType.Attachment)
            {
                ans.SelectedChoices.Clear();
                var selected = (input.ChoiceIds ?? new List<Guid>())
                    .Distinct()
                    .Where(cid => q.Choices.Any(c => c.Id == cid))
                    .ToList();
                foreach (var cid in selected)
                {
                    ans.SelectedChoices.Add(new ExamAnswerChoice
                    {
                        Id = Guid.NewGuid(),
                        ExamAnswerId = ans.Id,
                        ChoiceId = cid
                    });
                }
            }
        }

        a.Status = ExamAssignmentStatus.InProgress;
        a.StartedAt ??= DateTime.UtcNow;
        a.MaxScore = (a.Exam?.Questions ?? new List<ExamQuestion>()).Sum(q => q.Points);

        if (submit)
        {
            foreach (var q in a.Exam?.Questions ?? new List<ExamQuestion>())
            {
                var ans = a.Answers.FirstOrDefault(x => x.QuestionId == q.Id);
                if (ans == null) continue;
                ans.AutoScore = 0m;

                if (q.Type is ExamQuestionType.OpenText or ExamQuestionType.Attachment)
                {
                    ans.AutoScore = 0m;
                }
                else
                {
                    var correct = q.Choices.Where(c => c.IsCorrect).Select(c => c.Id).ToHashSet();
                    var selected = ans.SelectedChoices.Select(sc => sc.ChoiceId).ToHashSet();
                    var ok = correct.Count > 0 && correct.SetEquals(selected);
                    ans.AutoScore = ok ? q.Points : 0m;
                }
            }

            a.Score = a.Answers.Sum(x => x.AutoScore + x.ManualScore);
            a.SubmittedAt = DateTime.UtcNow;

            var hasManualReview = (a.Exam?.Questions ?? new List<ExamQuestion>())
                .Any(q => q.Type is ExamQuestionType.OpenText or ExamQuestionType.Attachment);

            if (hasManualReview)
            {
                a.Status = ExamAssignmentStatus.Submitted;
            }
            else
            {
                a.Status = ExamAssignmentStatus.Graded;
                a.GradedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new ApiMessageDto(submit ? "Examen enviado." : "Avance guardado."));
    }
}
