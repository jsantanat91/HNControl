using System.ComponentModel.DataAnnotations;
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
[Route("api/mobile/viaticos")]
public class ViaticosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IModuleAccessService _moduleAccess;
    private readonly IFileStorage _storage;

    public ViaticosController(ApplicationDbContext db, IModuleAccessService moduleAccess, IFileStorage storage)
    {
        _db = db;
        _moduleAccess = moduleAccess;
        _storage = storage;
    }

    public record WeekListItem(Guid Id, DateTime WeekStartDate, ViaticWeekStatus Status, decimal TotalAmount, decimal BillableAmount, int EntriesCount);
    public record EntryItem(Guid Id, DateTime DayDate, ViaticCategory Category, string Description, decimal Amount, bool IsBillable, bool HasAttachment);
    public record WeekDetail(Guid Id, DateTime WeekStartDate, ViaticWeekStatus Status, decimal TotalAmount, decimal BillableAmount, DateTime? SubmittedAt, DateTime? ApprovedAt, List<EntryItem> Entries);

    public class UpsertEntryRequest
    {
        [Required] public DateTime DayDate { get; set; }
        [Required] public ViaticCategory Category { get; set; }
        [Required, MaxLength(300)] public string Description { get; set; } = "";
        [Range(0.01, 9999999)] public decimal Amount { get; set; }
        public bool IsBillable { get; set; }
    }

    public class UpsertEntryMultipartRequest
    {
        [Required] public DateTime DayDate { get; set; }
        [Required] public ViaticCategory Category { get; set; }
        [Required, MaxLength(300)] public string Description { get; set; } = "";
        [Range(0.01, 9999999)] public decimal Amount { get; set; }
        public bool IsBillable { get; set; }
        public IFormFile? AttachmentFile { get; set; }
        public IFormFile? PdfFile { get; set; }
    }

    [HttpGet("weeks")]
    [ProducesResponseType(typeof(List<WeekListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Weeks([FromQuery] int take = 30)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        take = Math.Clamp(take, 1, 100);

        var rows = await _db.ViaticWeeks
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.WeekStartDate)
            .Take(take)
            .Select(w => new WeekListItem(
                w.Id,
                w.WeekStartDate,
                w.Status,
                w.TotalAmount,
                w.BillableAmount,
                w.Entries.Count))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("weeks")]
    public async Task<IActionResult> EnsureWeek([FromBody] DateTime anyDayInWeek)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var monday = ToMonday(anyDayInWeek.Date);
        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == monday);
        if (week == null)
        {
            week = new ViaticWeek
            {
                UserId = userId,
                WeekStartDate = monday,
                Status = ViaticWeekStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ViaticWeeks.Add(week);
            await _db.SaveChangesAsync();
        }

        return Ok(new { week.Id });
    }

    [HttpGet("week/{id:guid}")]
    [ProducesResponseType(typeof(WeekDetail), StatusCodes.Status200OK)]
    public async Task<IActionResult> Week(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (week == null) return NotFound();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);
        await _db.SaveChangesAsync();

        var detail = new WeekDetail(
            week.Id,
            week.WeekStartDate,
            week.Status,
            week.TotalAmount,
            week.BillableAmount,
            week.SubmittedAt,
            week.ApprovedAt,
            week.Entries
                .OrderBy(e => e.DayDate)
                .Select(e => new EntryItem(
                    e.Id,
                    e.DayDate,
                    e.Category,
                    e.Description,
                    e.Amount,
                    e.IsBillable,
                    e.Attachment != null))
                .ToList());

        return Ok(detail);
    }

    [HttpPost("week/{id:guid}/entries")]
    public async Task<IActionResult> AddEntry(Guid id, [FromBody] UpsertEntryRequest req)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (week == null) return NotFound();

        if (week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
            return Conflict(new { message = "Semana enviada/aprobada: no se puede modificar." });

        var start = week.WeekStartDate.Date;
        var end = start.AddDays(6);
        if (req.DayDate.Date < start || req.DayDate.Date > end)
            return BadRequest(new { message = "Ese dia no cae dentro de la semana." });

        if (req.IsBillable)
            return BadRequest(new { message = "Para facturable usa el endpoint con archivo PDF." });

        var entry = new ViaticEntry
        {
            WeekId = week.Id,
            DayDate = req.DayDate.Date,
            Category = req.Category,
            Description = (req.Description ?? "").Trim(),
            Amount = req.Amount,
            IsBillable = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.ViaticEntries.Add(entry);
        week.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Gasto agregado." });
    }

    [HttpPost("week/{id:guid}/entries/upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> AddEntryWithUpload(Guid id, [FromForm] UpsertEntryMultipartRequest req)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (week == null) return NotFound();

        if (week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
            return Conflict(new { message = "Semana enviada/aprobada: no se puede modificar." });

        var start = week.WeekStartDate.Date;
        var end = start.AddDays(6);
        if (req.DayDate.Date < start || req.DayDate.Date > end)
            return BadRequest(new { message = "Ese dia no cae dentro de la semana." });

        var uploadedFile = req.AttachmentFile ?? req.PdfFile;

        if (req.IsBillable && (uploadedFile == null || uploadedFile.Length == 0))
            return BadRequest(new { message = "Si es facturable, adjunta factura (PDF o imagen)." });

        if (!req.IsBillable && uploadedFile != null && uploadedFile.Length > 0)
            return BadRequest(new { message = "El adjunto solo aplica para gastos facturables." });

        var entry = new ViaticEntry
        {
            WeekId = week.Id,
            DayDate = req.DayDate.Date,
            Category = req.Category,
            Description = (req.Description ?? "").Trim(),
            Amount = req.Amount,
            IsBillable = req.IsBillable,
            CreatedAt = DateTime.UtcNow
        };

        _db.ViaticEntries.Add(entry);

        if (req.IsBillable && uploadedFile != null)
        {
            var attachment = new ViaticAttachment
            {
                EntryId = entry.Id,
                OriginalFileName = Path.GetFileName(uploadedFile.FileName),
                UploadedAt = DateTime.UtcNow
            };

            var (path, size, contentType, originalName) = await _storage.SaveFileAsync(
                uploadedFile,
                $"viaticos/{week.Id}",
                attachment.Id.ToString("N"),
                new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".heic", ".heif" },
                20 * 1024L * 1024L);
            attachment.StoragePath = path;
            attachment.SizeBytes = size;
            attachment.ContentType = contentType;
            attachment.OriginalFileName = originalName;
            _db.ViaticAttachments.Add(attachment);
        }

        week.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Gasto agregado." });
    }

    [HttpPut("entries/{entryId:guid}")]
    public async Task<IActionResult> EditEntry(Guid entryId, [FromBody] UpsertEntryRequest req)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var entry = await _db.ViaticEntries
            .Include(e => e.Week)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry?.Week == null || entry.Week.UserId != userId) return NotFound();

        if (entry.Week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
            return Conflict(new { message = "Semana enviada/aprobada: no se puede modificar." });

        if (req.IsBillable && entry.Attachment == null)
            return BadRequest(new { message = "Para marcar facturable necesitas adjuntar factura (PDF o imagen)." });

        var start = entry.Week.WeekStartDate.Date;
        var end = start.AddDays(6);
        if (req.DayDate.Date < start || req.DayDate.Date > end)
            return BadRequest(new { message = "Ese dia no cae dentro de la semana." });

        entry.DayDate = req.DayDate.Date;
        entry.Category = req.Category;
        entry.Description = (req.Description ?? "").Trim();
        entry.Amount = req.Amount;
        entry.IsBillable = req.IsBillable;
        entry.Week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await ViaticTotalsHelper.RecalcWeekAsync(_db, entry.WeekId);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Gasto actualizado." });
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid entryId)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var entry = await _db.ViaticEntries
            .Include(e => e.Week)
            .Include(e => e.Attachment)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry?.Week == null || entry.Week.UserId != userId) return NotFound();
        if (entry.Week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
            return Conflict(new { message = "Semana enviada/aprobada: no se puede modificar." });

        if (entry.Attachment != null)
            _db.ViaticAttachments.Remove(entry.Attachment);

        var weekId = entry.WeekId;
        _db.ViaticEntries.Remove(entry);
        await _db.SaveChangesAsync();
        await ViaticTotalsHelper.RecalcWeekAsync(_db, weekId);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Gasto eliminado." });
    }

    [HttpPost("week/{id:guid}/submit")]
    public async Task<IActionResult> SubmitWeek(Guid id)
    {
        if (!await _moduleAccess.HasAccessAsync(User, AppModules.Viaticos))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var week = await _db.ViaticWeeks
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

        if (week == null) return NotFound();
        if (week.Status == ViaticWeekStatus.Approved) return Ok(new { message = "La semana ya esta aprobada." });

        var bad = week.Entries.Any(e => e.IsBillable && e.Attachment == null);
        if (bad) return BadRequest(new { message = "Hay gastos facturables sin archivo adjunto." });

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);
        week.Status = ViaticWeekStatus.Submitted;
        week.SubmittedAt = DateTime.UtcNow;
        week.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Semana enviada al admin." });
    }

    private static DateTime ToMonday(DateTime date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }
}
