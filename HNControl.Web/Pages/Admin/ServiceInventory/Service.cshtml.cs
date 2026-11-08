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
public class ServiceModel(ApplicationDbContext db, ServiceInventoryService inventory) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public ServiceInventoryItemInput Input { get; set; } = new();
    public List<SelectListItem> Clients { get; private set; } = new();
    public List<AccountOption> Accounts { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid? accountId)
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        if (Id.HasValue)
        {
            var x = await db.ServiceInventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == Id);
            if (x == null) return NotFound();
            Input = new ServiceInventoryItemInput {
                RecordId = x.Id, Version = x.Version, CarrierKey = x.CarrierKey, OtherCarrierName = x.OtherCarrierName, Name = x.Name,
                AccountId = x.AccountId, ClientId = x.ClientId, AccountNumber = x.AccountNumber, PlanName = x.PlanName, PlanType = x.PlanType,
                DeliveryType = x.DeliveryType, DeviceModel = x.DeviceModel, SerialNumber = x.SerialNumber, KitNumber = x.KitNumber,
                SpeedMbps = x.SpeedMbps, MonthlyCost = x.MonthlyCost, PhysicalLocation = x.PhysicalLocation, Lan = x.Lan, Ddns = x.Ddns,
                SupportPhone = x.SupportPhone, FixedIp = x.FixedIp, SubnetMask = x.SubnetMask, Gateway = x.Gateway, Dns = x.Dns,
                ContractMonths = x.ContractMonths, ContractStartsOn = x.ContractStartsOn, ContractEndsOn = x.ContractEndsOn, Notes = x.Notes, IsActive = x.IsActive
            };
        }
        else if (accountId.HasValue)
        {
            var account = await db.ServiceInventoryAccounts.AsNoTracking().Where(x => x.Id == accountId && x.IsActive).Select(x => new { x.CarrierKey, x.OtherCarrierName }).SingleOrDefaultAsync();
            if (account != null) { Input.AccountId = accountId; Input.CarrierKey = account.CarrierKey; Input.OtherCarrierName = account.OtherCarrierName; }
        }
        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        if (ModelState.IsValid)
        {
            var result = await inventory.SaveItemAsync(Input, Id, User);
            if (result.Success)
            {
                TempData["FlashOk"] = Id.HasValue ? "Servicio actualizado." : "Servicio registrado en el inventario.";
                return RedirectToPage("Index", new { Status = Input.IsActive ? "activos" : "inactivos" });
            }
            foreach (var error in result.Errors) ModelState.AddModelError(string.IsNullOrEmpty(error.Field) ? "" : "Input." + error.Field, error.Message);
        }
        await LoadOptionsAsync();
        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        Clients = await db.Clients.AsNoTracking().Where(x => !x.IsTemporaryLead && (x.IsActive || x.Id == Input.ClientId)).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.ClientCode + " · " + x.Name + (x.IsActive ? "" : " (inactivo)"), x.Id.ToString())).ToListAsync();
        Accounts = await db.ServiceInventoryAccounts.AsNoTracking().Where(x => x.IsActive || x.Id == Input.AccountId).OrderBy(x => x.Label)
            .Select(x => new AccountOption(x.Id, x.CarrierKey, x.Label, x.Email, x.AccountNumber, x.IsActive, x.OtherCarrierName)).ToListAsync();
    }

    public record AccountOption(Guid Id, string CarrierKey, string Label, string Email, string AccountNumber, bool IsActive, string OtherCarrierName);
}
