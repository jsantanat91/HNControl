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
[Route("api/mobile/service-inventory")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ServiceInventoryController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid? clientId = null, string? carrier = null, int page = 1)
    {
        if (!AppRoles.IsGlobalAdmin(User) && !User.IsInRole(AppRoles.Employee)) return Forbid();
        page = Math.Clamp(page, 1, 100000);
        var query = db.ServiceInventoryItems.AsNoTracking();
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId);
        if (!string.IsNullOrWhiteSpace(carrier)) query = query.Where(x => x.CarrierKey == carrier);
        var total = await query.CountAsync();
        var admin = AppRoles.IsGlobalAdmin(User) || User.IsInRole(AppRoles.Employee);
        // Account secrets are available only through the separate internal-user action.
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new { x.Id, x.Name, x.CarrierKey, x.OtherCarrierName,
                Client = x.Client == null ? "Sin cliente asignado" : x.Client.ClientCode + " · " + x.Client.Name,
                x.AccountId, x.AccountNumber, x.MonthlyCost, x.Notes, x.ContractMonths, x.ContractStartsOn,
                AccountLabel = x.Account == null ? "" : x.Account.Label,
                AccountEmail = x.Account == null ? "" : x.Account.Email,
                ProviderAccountNumber = x.Account == null ? "" : x.Account.AccountNumber,
                AccountNotes = x.Account == null ? "" : x.Account.Notes,
                AccountIsActive = x.Account != null && x.Account.IsActive,
                CanCopyPassword = admin && x.Account != null && x.Account.PasswordProtected != "",
                x.PlanName, x.PlanType, x.DeliveryType, x.SpeedMbps, x.DeviceModel, x.SerialNumber,
                KitNumber = x.CarrierKey == "starlink" ? x.KitNumber : "", x.PhysicalLocation,
                x.IsActive, SupportPhone = x.CarrierKey == "starlink" ? "Online" : x.SupportPhone,
                x.FixedIp, x.SubnetMask, x.Gateway, x.Dns, x.Lan, x.Ddns, x.ContractEndsOn }).ToListAsync();
        var clients = await db.Clients.AsNoTracking().Where(c => db.ServiceInventoryItems.Any(x => x.ClientId == c.Id))
            .OrderBy(c => c.Name).Select(c => new { Id = c.Id.ToString(), Label = c.ClientCode + " · " + c.Name }).ToListAsync();
        var carriers = ServiceInventoryCatalog.Carriers.Select(c => new { Id = c.Id, Label = c.Name });
        return Ok(new { Items = items, Clients = clients, Carriers = carriers, Total = total, Page = page });
    }

    [HttpPost("{id:guid}/account-password")]
    public async Task<IActionResult> CopyPassword(Guid id, [FromServices] ServiceInventoryService inventory)
    {
        if (!AppRoles.IsGlobalAdmin(User) && !User.IsInRole(AppRoles.Employee)) return Forbid();
        var accountId = await db.ServiceInventoryItems.AsNoTracking().Where(x => x.Id == id).Select(x => x.AccountId).SingleOrDefaultAsync();
        if (!accountId.HasValue) return NotFound(new { message = "El servicio no tiene una cuenta vinculada." });
        var password = await inventory.CopyPasswordAsync(accountId.Value, User, allowEmployee: true);
        return string.IsNullOrEmpty(password)
            ? BadRequest(new { message = "La contraseña no está disponible. Revisa la cuenta y la clave de protección del servidor." })
            : Ok(new { password });
    }
}
