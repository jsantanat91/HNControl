using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
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

    public ServiceOrdersController(ApplicationDbContext db)
    {
        _db = db;
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
        DateTime CreatedAt,
        DateTime? StartedAt,
        DateTime? EstimatedEndDate,
        string LevantamientoNotes,
        string MaterialesNotes);

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
                o.Status != ServiceOrderStatus.InReview && o.Status != ServiceOrderStatus.Finalized && o.Status != ServiceOrderStatus.Completed,
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
            o.CreatedAt,
            o.StartedAt,
            o.EstimatedEndDate,
            o.LevantamientoNotes,
            o.MaterialesNotes
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
}
