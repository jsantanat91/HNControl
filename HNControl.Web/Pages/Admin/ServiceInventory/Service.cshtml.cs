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
    public string? LegacyPlanType { get; private set; }
    public string? LegacyDeliveryType { get; private set; }
    public string? LegacyCarrierKey { get; private set; }
    public string? SuggestedPlanType => LegacyCarrierKey != "starlink" && LegacyDeliveryType is "Dedicado" or "Best effort" ? LegacyDeliveryType : null;
    public bool HasLegacyContractEnd { get; private set; }
    public DateOnly? StoredContractEnd { get; private set; }
    public bool HasStoredNetworkDetails { get; private set; }

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
                ContractMonths = x.ContractMonths, ContractStartsOn = x.ContractStartsOn, ContractEndsOn = x.ContractEndsOn, Notes = x.Notes, IsActive = x.IsActive,
                HasNetworkDetails = ServiceInventoryDisplay.HasNetworkDetails(x)
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
        // Los campos calculados o fijos no aceptan sobrescrituras del formulario.
        if (Input.CarrierKey == "starlink")
        {
            Input.DeliveryType = "Satelital";
            Input.SupportPhone = "Online";
            ModelState.Remove("Input.DeliveryType");
            ModelState.Remove("Input.SupportPhone");
        }
        ModelState.Remove("Input.ContractEndsOn");
        await LoadOptionsAsync();
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
        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        ServiceInventoryItem? previous = null;
        if (Id.HasValue)
        {
            previous = await db.ServiceInventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == Id);
            if (previous != null)
            {
                LegacyCarrierKey = previous.CarrierKey;
                LegacyPlanType = !string.IsNullOrEmpty(previous.PlanType) && !ServiceInventoryDisplay.GetPlanTypes(previous.CarrierKey).Contains(previous.PlanType) ? previous.PlanType : null;
                LegacyDeliveryType = !string.IsNullOrEmpty(previous.DeliveryType) && !ServiceInventoryDisplay.GetDeliveryTypes(previous.CarrierKey).Contains(previous.DeliveryType) ? previous.DeliveryType : null;
                StoredContractEnd = previous.ContractEndsOn;
                HasLegacyContractEnd = StoredContractEnd.HasValue && (!previous.ContractStartsOn.HasValue || !previous.ContractMonths.HasValue);
                HasStoredNetworkDetails = ServiceInventoryDisplay.HasNetworkDetails(previous);
                Input.ContractEndsOn = HasLegacyContractEnd && Input.ContractStartsOn == previous.ContractStartsOn && Input.ContractMonths == previous.ContractMonths
                    ? previous.ContractEndsOn : null;
            }
        }
        if (!Input.HasNetworkDetails)
        {
            // Al cerrar la sección, los controles deshabilitados no se envían. Si otro dato
            // falla, restaurar los valores guardados evita que reabrirla borre la red sin querer.
            Input.Lan = previous?.Lan;
            Input.Ddns = previous?.Ddns;
            Input.FixedIp = previous?.FixedIp;
            Input.SubnetMask = previous?.SubnetMask;
            Input.Gateway = previous?.Gateway;
            Input.Dns = previous?.Dns;
            foreach (var name in new[] { nameof(Input.Lan), nameof(Input.Ddns), nameof(Input.FixedIp), nameof(Input.SubnetMask), nameof(Input.Gateway), nameof(Input.Dns) })
                ModelState.Remove("Input." + name);
        }
        if (Input.ContractStartsOn.HasValue && Input.ContractMonths.HasValue)
            Input.ContractEndsOn = ServiceInventoryDisplay.CalculateContractEnd(Input.ContractStartsOn, Input.ContractMonths);
        if (Input.CarrierKey == "starlink")
        {
            Input.DeliveryType = "Satelital";
            Input.SupportPhone = "Online";
        }
        Clients = await db.Clients.AsNoTracking().Where(x => !x.IsTemporaryLead && (x.IsActive || x.Id == Input.ClientId)).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.ClientCode + " · " + x.Name + (x.IsActive ? "" : " (inactivo)"), x.Id.ToString())).ToListAsync();
        Accounts = await db.ServiceInventoryAccounts.AsNoTracking().Where(x => x.IsActive || x.Id == Input.AccountId).OrderBy(x => x.Label)
            .Select(x => new AccountOption(x.Id, x.CarrierKey, x.Label, x.Email, x.AccountNumber, x.IsActive, x.OtherCarrierName)).ToListAsync();
    }

    public record AccountOption(Guid Id, string CarrierKey, string Label, string Email, string AccountNumber, bool IsActive, string OtherCarrierName);
}
