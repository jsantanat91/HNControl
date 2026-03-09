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
[Route("api/mobile/orders")]
public class ServiceOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IServiceOrderPdfRenderer _pdf;

    public ServiceOrdersController(ApplicationDbContext db, IFileStorage storage, IServiceOrderPdfRenderer pdf)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
    }

    public record OrderListItem(
        Guid Id,
        string Client,
        string Title,
        ServiceOrderType Type,
        ServiceOrderStatus Status,
        ServiceOrderWorkflowArea CurrentArea,
        string ClaimedBy,
        bool IsMine,
        bool CanTake,
        DateTime CreatedAt,
        DateTime? EstimatedEndDate);

    public record OrderDetail(
        Guid Id,
        string Client,
        string Title,
        string Description,
        ServiceOrderType Type,
        ServiceOrderStatus Status,
        ServiceOrderWorkflowArea CurrentArea,
        string ClaimedBy,
        bool IsMine,
        bool CanEdit,
        DateTime CreatedAt,
        DateTime? StartedAt,
        DateTime? EstimatedEndDate,
        string LevantamientoNotes,
        string MaterialesNotes,
        List<OrderEvidenceItem> Evidences);

    public record OrderEvidenceItem(Guid Id, string OriginalFileName, string UploadedAtLocal);

    public class OrderNotesUpdateRequest
    {
        public string? LevantamientoNotes { get; set; }
        public string? MaterialesNotes { get; set; }
    }

    public class UploadEvidenceRequest
    {
        public IFormFile? EvidenceFile { get; set; }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OrderListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int take = 100)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        take = Math.Clamp(take, 1, 300);

        var rows = await _db.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Include(o => o.ClaimedByEmployee)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .Select(o => new OrderListItem(
                o.Id,
                o.Client != null ? o.Client.Name : "-",
                o.Title,
                o.Type,
                o.Status,
                o.CurrentArea,
                o.ClaimedByEmployee != null ? o.ClaimedByEmployee.FullName : "Sin tomar",
                o.ClaimedByUserId == userId,
                (string.IsNullOrWhiteSpace(o.ClaimedByUserId) || o.ClaimedByUserId == userId) &&
                o.Status != ServiceOrderStatus.InReview &&
                o.Status != ServiceOrderStatus.Finalized &&
                o.Status != ServiceOrderStatus.Completed,
                o.CreatedAt,
                o.EstimatedEndDate
            ))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid id)
    {
        var o = await _db.ServiceOrders
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.ClaimedByEmployee)
            .Include(x => x.Evidences)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (o == null) return NotFound();

        return Ok(new OrderDetail(
            o.Id,
            o.Client?.Name ?? "-",
            o.Title,
            o.Description,
            o.Type,
            o.Status,
            o.CurrentArea,
            o.ClaimedByEmployee?.FullName ?? "Sin tomar",
            o.ClaimedByUserId == (User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""),
            CanEdit(o, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""),
            o.CreatedAt,
            o.StartedAt,
            o.EstimatedEndDate,
            o.LevantamientoNotes,
            o.MaterialesNotes,
            o.Evidences
                .OrderByDescending(e => e.UploadedAt)
                .Select(e => new OrderEvidenceItem(
                    e.Id,
                    e.OriginalFileName,
                    e.UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")))
                .ToList()
        ));
    }

    [HttpPost("{id:guid}/take")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Take(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();

        if (o.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
            return Conflict(new { message = "La orden ya no acepta edicion" });

        if (!string.IsNullOrWhiteSpace(o.ClaimedByUserId) && o.ClaimedByUserId != userId)
            return Conflict(new { message = "La orden ya fue tomada por otro tÃ©cnico. Pide al admin desasignarla." });

        o.ClaimedByUserId = userId;
        o.ClaimedAt = DateTime.UtcNow;

        if (o.Status == ServiceOrderStatus.Created)
        {
            o.Status = ServiceOrderStatus.InProgress;
            o.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Orden tomada" });
    }

    [HttpPut("{id:guid}/notes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] OrderNotesUpdateRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (!CanEdit(o, userId)) return Forbid();

        o.LevantamientoNotes = TrimMax(req.LevantamientoNotes, 4000);
        o.MaterialesNotes = TrimMax(req.MaterialesNotes, 4000);
        if (o.Status == ServiceOrderStatus.Created)
        {
            o.Status = ServiceOrderStatus.InProgress;
            o.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Notas guardadas." });
    }

    [HttpPost("{id:guid}/area/next")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> NextArea(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (!CanEdit(o, userId)) return Forbid();

        var current = (int)o.CurrentArea;
        var max = (int)ServiceOrderWorkflowArea.CierreTecnico;
        if (current < max)
            o.CurrentArea = (ServiceOrderWorkflowArea)(current + 1);

        if (o.Status == ServiceOrderStatus.Created)
        {
            o.Status = ServiceOrderStatus.InProgress;
            o.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Ãrea actualizada.", area = o.CurrentArea });
    }

    [HttpPost("{id:guid}/area/previous")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviousArea(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (!CanEdit(o, userId)) return Forbid();

        var current = (int)o.CurrentArea;
        var min = (int)ServiceOrderWorkflowArea.Levantamiento;
        if (current > min)
            o.CurrentArea = (ServiceOrderWorkflowArea)(current - 1);

        await _db.SaveChangesAsync();
        return Ok(new { message = "Ãrea actualizada.", area = o.CurrentArea });
    }

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (!CanEdit(o, userId)) return Forbid();

        if (o.CurrentArea != ServiceOrderWorkflowArea.CierreTecnico)
            return Conflict(new { message = "Debes avanzar hasta Cierre tÃ©cnico para enviar a revisiÃ³n." });

        if (o.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
            return Conflict(new { message = "La orden ya no permite envÃ­o a revisiÃ³n." });

        o.Status = ServiceOrderStatus.InReview;
        o.SubmittedForReviewAt = DateTime.UtcNow;
        o.PdfStoragePath = null;
        o.PdfGeneratedAt = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Orden enviada a revisiÃ³n." });
    }

    [HttpPost("{id:guid}/evidence")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadEvidence(Guid id, [FromForm] UploadEvidenceRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (!CanEdit(o, userId)) return Forbid();

        if (req.EvidenceFile == null || req.EvidenceFile.Length == 0)
            return BadRequest(new { message = "Selecciona un archivo vÃ¡lido." });

        var (path, size, contentType, originalName) = await _storage.SaveFileAsync(
            req.EvidenceFile,
            $"serviceorders/{o.Id}/evidence",
            Guid.NewGuid().ToString("N"),
            new[] { ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".heic", ".heif" },
            25 * 1024L * 1024L);

        _db.ServiceOrderEvidences.Add(new ServiceOrderEvidence
        {
            OrderId = o.Id,
            OriginalFileName = originalName,
            ContentType = contentType,
            SizeBytes = size,
            StoragePath = path,
            UploadedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Evidencia adjuntada." });
    }

    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id)
    {
        var o = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();

        if (string.IsNullOrWhiteSpace(o.PdfStoragePath))
        {
            var bytes = await _pdf.RenderAsync(o);
            var fileName = $"orden_{o.Id:N}.pdf";
            var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{o.Id}/pdf", fileName, "application/pdf");
            o.PdfStoragePath = path;
            o.PdfGeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (string.IsNullOrWhiteSpace(o.PdfStoragePath))
            return NotFound(new { message = "La orden no tiene PDF disponible." });

        var downloadName = $"OrdenServicio_{o.Id:N}.pdf";
        var (stream, contentType, _) = await _storage.OpenAsync(o.PdfStoragePath, downloadName);
        return File(stream, contentType, downloadName);
    }

    private bool CanEdit(ServiceOrder o, string userId)
    {
        if (o.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
            return false;

        if (AppRoles.IsGlobalAdmin(User))
            return true;

        return !string.IsNullOrWhiteSpace(userId) && o.ClaimedByUserId == userId;
    }

    private static string TrimMax(string? value, int max)
    {
        var v = (value ?? string.Empty).Trim();
        return v.Length <= max ? v : v[..max];
    }
}

