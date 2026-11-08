using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceInventory;

[Authorize(Policy = "AdminOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(ApplicationDbContext db, ServiceInventoryService inventory) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "servicios";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Carrier { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ClientId { get; set; }
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "activos";
    [BindProperty(SupportsGet = true)] public string? Assignment { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [TempData] public string? FlashOk { get; set; }
    public List<ServiceInventoryItem> Items { get; private set; } = new();
    public List<AccountRow> Accounts { get; private set; } = new();
    public List<SelectListItem> Clients { get; private set; } = new();
    public int ActiveCount { get; private set; }
    public int AvailableCount { get; private set; }
    public int UnpricedCount { get; private set; }
    public decimal MonthlyCost { get; private set; }
    public int ExpiringCount { get; private set; }
    public int Total { get; private set; }
    public int Pages => Math.Max(1, (Total + 23) / 24);
    public DateOnly Today => DateOnly.FromDateTime(AppTime.Today);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        Tab = Tab == "cuentas" ? "cuentas" : "servicios";
        var active = db.ServiceInventoryItems.AsNoTracking().Where(x => x.IsActive);
        ActiveCount = await active.CountAsync();
        AvailableCount = await active.CountAsync(x => x.ClientId == null);
        UnpricedCount = await active.CountAsync(x => x.MonthlyCost == null);
        // Decimal Sum no es compatible con SQLite; la proyección también evita cargar datos de red.
        MonthlyCost = (await active.Select(x => x.MonthlyCost).ToListAsync()).Sum(x => x ?? 0);
        var end = Today.AddDays(30);
        ExpiringCount = await active.CountAsync(x => x.ContractEndsOn >= Today && x.ContractEndsOn <= end);
        Clients = await db.Clients.AsNoTracking().Where(x => !x.IsTemporaryLead).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.ClientCode + " · " + x.Name, x.Id.ToString())).ToListAsync();
        var query = Q?.Trim().ToLowerInvariant();
        if (Tab == "cuentas")
        {
            var accounts = db.ServiceInventoryAccounts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(Carrier)) accounts = accounts.Where(x => x.CarrierKey == Carrier);
            if (Status == "activos") accounts = accounts.Where(x => x.IsActive);
            if (Status == "inactivos") accounts = accounts.Where(x => !x.IsActive);
            if (!string.IsNullOrEmpty(query)) accounts = accounts.Where(x => x.Label.ToLower().Contains(query) || x.Email.ToLower().Contains(query) || x.AccountNumber.ToLower().Contains(query));
            Total = await accounts.CountAsync();
            P = Math.Clamp(P, 1, Pages);
            Accounts = await accounts.OrderBy(x => x.CarrierKey).ThenBy(x => x.Label).ThenBy(x => x.Id).Skip((P - 1) * 24).Take(24)
                .Select(x => new AccountRow(x.Id, x.CarrierKey, x.OtherCarrierName, x.Label, x.Email, x.AccountNumber, x.IsActive,
                    x.PasswordProtected != "", x.Services.Count, x.Services.Count(s => s.IsActive))).ToListAsync();
        }
        else
        {
            var items = db.ServiceInventoryItems.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(Carrier)) items = items.Where(x => x.CarrierKey == Carrier);
            if (ClientId.HasValue) items = items.Where(x => x.ClientId == ClientId);
            if (Status == "activos") items = items.Where(x => x.IsActive);
            if (Status == "inactivos") items = items.Where(x => !x.IsActive);
            if (Assignment == "asignados") items = items.Where(x => x.ClientId != null);
            if (Assignment == "disponibles") items = items.Where(x => x.ClientId == null);
            if (Assignment == "porvencer") items = items.Where(x => x.ContractEndsOn >= Today && x.ContractEndsOn <= end);
            if (!string.IsNullOrEmpty(query)) items = items.Where(x => x.Name.ToLower().Contains(query) || x.SerialNumber.ToLower().Contains(query) || x.KitNumber.ToLower().Contains(query)
                || x.AccountNumber.ToLower().Contains(query) || x.PhysicalLocation.ToLower().Contains(query) || (x.Client != null && x.Client.Name.ToLower().Contains(query)));
            Total = await items.CountAsync();
            P = Math.Clamp(P, 1, Pages);
            // No se incluye Account: sus credenciales nunca se cargan para construir el listado.
            Items = await items.Include(x => x.Client).OrderBy(x => x.CarrierKey).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((P - 1) * 24).Take(24).ToListAsync();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostCopyPasswordAsync(Guid id)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        var password = await inventory.CopyPasswordAsync(id, User);
        return password == null ? new NotFoundObjectResult(new { message = "No se pudo recuperar la contraseña de esta cuenta." }) : new JsonResult(new { password });
    }

    public record AccountRow(Guid Id, string CarrierKey, string OtherCarrierName, string Label, string Email, string AccountNumber,
        bool IsActive, bool HasPassword, int Services, int ActiveServices);
}
