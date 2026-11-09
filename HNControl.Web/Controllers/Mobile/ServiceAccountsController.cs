using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Controllers.Mobile;
[ApiController, Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/mobile/service-accounts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ServiceAccountsController(ApplicationDbContext db, ServiceInventoryService inventory) : ControllerBase
{
    private bool Allowed => AppRoles.IsGlobalAdmin(User) || User.IsInRole(AppRoles.Employee);
    [HttpGet]
    public async Task<IActionResult> List(string? search = null, int page = 1)
    {
        if (!Allowed) return Forbid();
        page = Math.Max(1, page);
        var query = db.ServiceInventoryAccounts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Label.ToLower().Contains(term) || x.Email.ToLower().Contains(term)
                || x.AccountNumber.ToLower().Contains(term) || x.CarrierKey.ToLower().Contains(term));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Label).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new { x.Id, x.Label, x.CarrierKey, x.OtherCarrierName, x.Email, x.AccountNumber, x.Notes, x.IsActive,
                CanCopyPassword = x.PasswordProtected != "", ServiceCount = x.Services.Count }).ToListAsync();
        return Ok(new { Items = items, Total = total, Page = page });
    }
    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> Password(Guid id)
    {
        if (!Allowed) return Forbid();
        if (!await db.ServiceInventoryAccounts.AnyAsync(x => x.Id == id)) return NotFound();
        var password = await inventory.CopyPasswordAsync(id, User, allowEmployee: true);
        return string.IsNullOrEmpty(password) ? BadRequest(new { message = "No hay contraseña disponible para esta cuenta. Consulta al administrador." }) : Ok(new { password });
    }
}
