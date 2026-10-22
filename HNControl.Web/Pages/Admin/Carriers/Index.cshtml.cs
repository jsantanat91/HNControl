using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Carriers;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<InternetCarrier> Carriers { get; set; } = new();
    [TempData] public string? FlashOk { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!User.IsInRole(AppRoles.Admin)) return Forbid();
        var carrier = await _db.InternetCarriers.FindAsync(id);
        if (carrier == null) return NotFound();
        if (await _db.ClientCarrierServices.AnyAsync(s => s.CarrierId == id))
        {
            FlashError = "Este carrier tiene servicios ligados. Elimina o reasigna esos servicios antes de eliminarlo.";
            return RedirectToPage();
        }
        _db.InternetCarriers.Remove(carrier);
        try
        {
            await _db.SaveChangesAsync();
            FlashOk = "Carrier eliminado.";
        }
        catch (DbUpdateException)
        {
            FlashError = "No se pudo eliminar el carrier. Revisa si otro registro lo está utilizando.";
        }
        return RedirectToPage();
    }

    public async Task OnGetAsync()
    {
        Carriers = await _db.InternetCarriers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}
