using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Controllers.Integrations;

[ApiController, AllowAnonymous, ServiceFilter(typeof(SupportIntegrationAuth))]
[Route("api/integrations/support"), EnableRateLimiting("support-integration")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SupportController(ApplicationDbContext db, IConfiguration config, ServiceOrderCreationNotifications notifications) : ControllerBase
{
    private string Source => config["SupportIntegration:SourceId"] ?? "hn-support";

    // Explicit allowlist: never serialize entities containing account passwords, notes or costs.
    private static IQueryable<ServiceDto> Services(IQueryable<ServiceInventoryItem> query) => query.Select(x => new ServiceDto(
        x.Id,x.Name,x.IsActive,x.CarrierKey,x.OtherCarrierName,x.ClientId,x.Client == null ? "" : x.Client.Name,
        x.Client == null ? "" : x.Client.ClientCode,x.AccountId,x.Account == null ? "" : x.Account.Label,
        x.Account == null ? "" : x.Account.AccountNumber,x.Account != null && x.Account.IsActive,
        x.AccountNumber,x.SerialNumber,x.KitNumber,x.DeviceModel,x.PlanName,x.SpeedMbps,x.PhysicalLocation,
        x.SupportPhone,x.Lan,x.Ddns,x.FixedIp,x.UpdatedAt));

    [HttpGet("services")]
    public async Task<IActionResult> List(string? q = null, bool activeOnly = true, int page = 1, int pageSize = 20)
    {
        page = Math.Clamp(page,1,100000); pageSize = Math.Clamp(pageSize,1,100);
        // Filter entities before projection to keep EF translation portable.
        var ids = db.ServiceInventoryItems.AsNoTracking().Where(x => !activeOnly || x.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) {
            var term = q.Trim().ToLowerInvariant(); if (term.Length > 150) return BadRequest(new { message = "Búsqueda demasiado larga." });
            ids = ids.Where(x => x.Name.ToLower().Contains(term) || x.SerialNumber.ToLower().Contains(term)
                || x.AccountNumber.ToLower().Contains(term) || x.CarrierKey.ToLower().Contains(term)
                || (x.Client != null && x.Client.Name.ToLower().Contains(term))
                || (x.Account != null && (x.Account.AccountNumber.ToLower().Contains(term) || x.Account.Label.ToLower().Contains(term))));
        }
        var total = await ids.CountAsync();
        var selected = await ids.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page-1)*pageSize).Take(pageSize).Select(x => x.Id).ToListAsync();
        var items = await Services(db.ServiceInventoryItems.AsNoTracking().Where(x => selected.Contains(x.Id))).ToListAsync();
        return Ok(new { items = items.OrderBy(x => selected.IndexOf(x.Id)), total, page, pageSize, fetchedAt = DateTime.UtcNow });
    }
    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> Service(Guid id) { var item = await Services(db.ServiceInventoryItems.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(); return item == null ? NotFound() : Ok(item); }

    [HttpGet("order-options")]
    public async Task<IActionResult> Options() => Ok(new {
        clients = await db.Clients.AsNoTracking().Where(x => x.IsActive && !x.IsTemporaryLead).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.ClientCode }).ToListAsync(),
        technicians = await db.EmployeeProfiles.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new { id = x.UserId, name = x.FullName }).ToListAsync(),
    });

    private async Task<object?> ReadOrder(long ticketId) {
        var link = await db.SupportOrderLinks.AsNoTracking().Include(x => x.Order).SingleOrDefaultAsync(x => x.SourceId == Source && x.TicketId == ticketId);
        if (link?.Order == null) return null;
        return new { id = link.OrderId, ticketId, title = link.Order.Title, status = link.Order.Status.GetDisplayName(),
            createdAt = link.CreatedAt, path = $"/Admin/ServiceOrders/Details/{link.OrderId}" };
    }
    [HttpGet("orders/{ticketId:long}")]
    public async Task<IActionResult> Order(long ticketId) => Ok(new { order = await ReadOrder(ticketId) });

    [HttpPost("orders")]
    public async Task<IActionResult> Create(CreateOrderInput input) {
        if (input.TicketId <= 0 || input.ClientId == Guid.Empty || input.Title.Trim().Length < 5 || Source.Length > 80) return BadRequest(new { message = "Datos de orden inválidos." });
        var previous = await ReadOrder(input.TicketId);
        if (previous != null) return Ok(new { order = previous, reused = true });
        if (!await db.Clients.AnyAsync(x => x.Id == input.ClientId && x.IsActive && !x.IsTemporaryLead)) return BadRequest(new { message = "Selecciona un cliente activo de Control." });
        if (input.ServiceId.HasValue) {
            var service = await db.ServiceInventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ServiceId && x.IsActive);
            if (service == null || (service.ClientId.HasValue && service.ClientId != input.ClientId)) return BadRequest(new { message = "El servicio no corresponde al cliente seleccionado o está inactivo." });
        }
        if (input.Type is not (ServiceOrderType.Correctivo or ServiceOrderType.Preventivo or ServiceOrderType.LevantamientoTecnico)) return BadRequest(new { message = "Tipo de orden inválido." });
        if (!string.IsNullOrEmpty(input.AssignedUserId) && !await db.EmployeeProfiles.AnyAsync(x => x.UserId == input.AssignedUserId && x.IsActive)) return BadRequest(new { message = "Selecciona un técnico activo." });
        if (input.StartDate == default || input.EndDate < input.StartDate || input.EndDate.DayNumber-input.StartDate.DayNumber > 365) return BadRequest(new { message = "Revisa las fechas de atención." });
        var order = new ServiceOrder {
            ClientId = input.ClientId, Title = input.Title.Trim(), Type = input.Type, Status = ServiceOrderStatus.Created,
            Description = $"Origen: HN Support · HN-{input.TicketId:D6}\n{input.Description.Trim()}",
            AssignedUserId = string.IsNullOrWhiteSpace(input.AssignedUserId) ? null : input.AssignedUserId,
            StartedAt = DateTime.SpecifyKind(input.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            EstimatedEndDate = DateTime.SpecifyKind(input.EndDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            DeliveryJson = new HNControl.Shared.OrderDeliveryData { Kind = "service" }.Serialize(),
            PublicToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow, CurrentArea = ServiceOrderWorkflowArea.Levantamiento
        };
        // The unique source/ticket link is committed with the order before any notifications.
        await using (var tx = await db.Database.BeginTransactionAsync()) {
            db.ServiceOrders.Add(order);
            var template = await db.ServiceOrderChecklistTemplates.Include(x => x.Items).Where(x => x.IsActive && x.Type == input.Type).OrderBy(x => x.Name).FirstOrDefaultAsync();
            if (template != null) foreach(var item in template.Items.OrderBy(x => x.SortOrder)) order.Checklist.Add(new ServiceOrderChecklistItem { OrderId = order.Id, SortOrder = item.SortOrder, Category = item.Category, Title = item.Title, IsRequired = item.IsRequired, IsDone = false, Notes = "" });
            db.SupportOrderLinks.Add(new SupportOrderLink { SourceId = Source, TicketId = input.TicketId, OrderId = order.Id, InventoryServiceId = input.ServiceId, RequestedBy = input.RequestedBy });
            notifications.QueueCreated(order);
            try { await db.SaveChangesAsync(); await tx.CommitAsync(); }
            catch (DbUpdateException) {
                await tx.RollbackAsync(); db.ChangeTracker.Clear();
                var concurrent = await ReadOrder(input.TicketId);
                if (concurrent != null) return Ok(new { order = concurrent, reused = true });
                throw;
            }
        }
        var notice = await notifications.NotifyCreatedAsync(order.Id);
        return StatusCode(201,new { order = await ReadOrder(input.TicketId), reused = false, notice = notice.UserMessage });
    }
}

public sealed record ServiceDto(Guid Id,string Name,bool IsActive,string CarrierKey,string OtherCarrierName,Guid? ClientId,string ClientName,
    string ClientCode,Guid? AccountId,string AccountLabel,string ProviderAccountNumber,bool AccountIsActive,string ServiceNumber,
    string SerialNumber,string KitNumber,string DeviceModel,string PlanName,decimal? SpeedMbps,string PhysicalLocation,
    string SupportPhone,string Lan,string Ddns,string FixedIp,DateTime UpdatedAt);
public sealed class CreateOrderInput {
    public long TicketId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ServiceId { get; set; }
    [Required,MaxLength(200)] public string Title { get; set; } = "";
    [Required,MaxLength(1800)] public string Description { get; set; } = "";
    [MaxLength(200)] public string RequestedBy { get; set; } = "";
    [MaxLength(450)] public string? AssignedUserId { get; set; }
    public ServiceOrderType Type { get; set; } = ServiceOrderType.Correctivo;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
