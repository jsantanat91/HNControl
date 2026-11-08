using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceInventory;

[Authorize(Policy = "AdminOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountModel(ApplicationDbContext db, ServiceInventoryService inventory) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public ServiceInventoryAccountInput Input { get; set; } = new();
    [BindProperty] public string? Password { get; set; }
    public int ServicesCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? carrier = null)
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        if (Id.HasValue)
        {
            var input = await db.ServiceInventoryAccounts.AsNoTracking().Where(x => x.Id == Id).Select(x => new ServiceInventoryAccountInput {
                RecordId = x.Id, Version = x.Version, CarrierKey = x.CarrierKey, OtherCarrierName = x.OtherCarrierName, Label = x.Label,
                Email = x.Email, AccountNumber = x.AccountNumber, Notes = x.Notes, IsActive = x.IsActive }).SingleOrDefaultAsync();
            if (input == null) return NotFound();
            Input = input;
            ServicesCount = await db.ServiceInventoryItems.CountAsync(x => x.AccountId == Id);
        }
        else if (ServiceInventoryCatalog.Carriers.Any(x => x.Id == carrier))
            Input.CarrierKey = carrier!;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        if (ModelState.IsValid)
        {
            var result = await inventory.SaveAccountAsync(Input, Id, Password, User);
            Password = null;
            ModelState.SetModelValue(nameof(Password), "", "");
            if (result.Success)
            {
                TempData["FlashOk"] = Id.HasValue ? "Cuenta actualizada." : "Cuenta registrada. Su contraseña está protegida y disponible solo para copiar.";
                return RedirectToPage("Index", new { Tab = "cuentas", Status = Input.IsActive ? "activos" : "inactivos" });
            }
            foreach (var error in result.Errors) ModelState.AddModelError(error.Field == "Password" ? "Password" : string.IsNullOrEmpty(error.Field) ? "" : "Input." + error.Field, error.Message);
        }
        Password = null;
        ModelState.SetModelValue(nameof(Password), "", "");
        ServicesCount = Id.HasValue ? await db.ServiceInventoryItems.CountAsync(x => x.AccountId == Id) : 0;
        return Page();
    }
}
