using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Locations;

[Authorize(Policy = "InventorySupervisor")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<InventoryLocation> Locations { get; set; } = new();

    [BindProperty, Required, MaxLength(200)]
    public string NewName { get; set; } = "";

    [TempData] public string? Info { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        Locations = await _db.InventoryLocations
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        var name = NewName.Trim();
        var key = name.ToLowerInvariant();

        var exists = await _db.InventoryLocations
            .AsNoTracking()
            .AnyAsync(x => x.Name.ToLower() == key);

        if (exists)
        {
            Error = "Esa ubicaciÃ³n ya existe.";
            return RedirectToPage();
        }

        _db.InventoryLocations.Add(new InventoryLocation
        {
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        Info = "UbicaciÃ³n agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var l = await _db.InventoryLocations.FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return NotFound();

        l.IsActive = !l.IsActive;
        l.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = l.IsActive ? "UbicaciÃ³n activada." : "UbicaciÃ³n desactivada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(Guid id, string name)
    {
        var l = await _db.InventoryLocations.FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return NotFound();

        var newName = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            Error = "El nombre no puede ir vacÃ­o.";
            return RedirectToPage();
        }

        var key = newName.ToLowerInvariant();
        var exists = await _db.InventoryLocations.AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Name.ToLower() == key);

        if (exists)
        {
            Error = "Ya existe otra ubicaciÃ³n con ese nombre.";
            return RedirectToPage();
        }

        l.Name = newName;
        l.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Info = "UbicaciÃ³n actualizada.";
        return RedirectToPage();
    }
}

