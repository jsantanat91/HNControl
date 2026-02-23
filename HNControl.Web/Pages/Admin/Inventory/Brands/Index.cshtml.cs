using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Brands;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<InventoryBrand> Brands { get; set; } = new();

    [BindProperty, Required, MaxLength(120)]
    public string NewName { get; set; } = "";

    [TempData] public string? Info { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        Brands = await _db.InventoryBrands
            .AsNoTracking()
            .OrderByDescending(b => b.IsActive)
            .ThenBy(b => b.Name)
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

        var exists = await _db.InventoryBrands
            .AsNoTracking()
            .AnyAsync(b => b.Name.ToLower() == key);

        if (exists)
        {
            Error = "Esa marca ya existe.";
            return RedirectToPage();
        }

        _db.InventoryBrands.Add(new InventoryBrand
        {
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        Info = "Marca agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var b = await _db.InventoryBrands.FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();

        b.IsActive = !b.IsActive;
        b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Info = b.IsActive ? "Marca activada." : "Marca desactivada.";
        return RedirectToPage();
    }
}
