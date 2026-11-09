using HNControl.Shared;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.ServiceOrders;

[Authorize]
public class DeliveryModel(ApplicationDbContext db) : PageModel
{
    public ServiceOrder Order { get; private set; } = default!;
    public bool CanEdit { get; private set; }
    [BindProperty] public OrderDeliveryData Input { get; set; } = new();
    [BindProperty, System.ComponentModel.DataAnnotations.DisplayFormat(ConvertEmptyStringToNull = false)] public string Expected { get; set; } = "";
    public string BackPage => AppRoles.IsGlobalAdmin(User) ? "/Admin/ServiceOrders/Details" : "/ServiceOrders/Work";
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!OrderDeliveryService.CanAccess(User)) return Forbid();
        if (!await Load(id)) return NotFound();
        Input = OrderDeliveryData.Read(Order.DeliveryJson);
        Expected = Order.DeliveryJson;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!OrderDeliveryService.CanAccess(User)) return Forbid();
        if (!await Load(id)) return NotFound();
        if (!CanEdit) return Forbid();
        // Empty strings and absent optional fields are expected in this form.
        if (!ModelState.IsValid) return Page();
        var error = await OrderDeliveryService.SaveAsync(db, id, User, Input, Expected ?? "");
        if (error != null) { ModelState.AddModelError("", error); return Page(); }
        TempData["Info"] = "Entrega y equipos guardados. Se incluirán en el PDF y las firmas de esta orden.";
        return RedirectToPage(new { id });
    }
    private async Task<bool> Load(Guid id)
    {
        Order = (await db.ServiceOrders.AsNoTracking().Include(x => x.Client).Include(x => x.ClientServiceContract)
            .Include(x => x.Signatures).SingleOrDefaultAsync(x => x.Id == id))!;
        if (Order == null) return false;
        CanEdit = OrderDeliveryService.CanEdit(Order, User);
        return true;
    }
}
