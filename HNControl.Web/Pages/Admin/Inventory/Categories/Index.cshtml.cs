using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Categories;

[Authorize(Policy = "InventorySupervisor")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<InventoryCategory> Categories { get; set; } = new();

    [BindProperty, Required, MaxLength(100)]
    public string NewName { get; set; } = "";

    [TempData] public string? Info { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        Categories = await _db.InventoryCategories
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

        var exists = await _db.InventoryCategories
            .AsNoTracking()
            .AnyAsync(x => x.Name.ToLower() == key);

        if (exists)
        {
            Error = "Esa categorÃ­a ya existe.";
            return RedirectToPage();
        }

        _db.InventoryCategories.Add(new InventoryCategory
        {
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        Info = "CategorÃ­a agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var c = await _db.InventoryCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        c.IsActive = !c.IsActive;
        c.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Info = c.IsActive ? "CategorÃ­a activada." : "CategorÃ­a desactivada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(Guid id, string name)
    {
        var c = await _db.InventoryCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        var newName = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            Error = "El nombre no puede ir vacÃ­o.";
            return RedirectToPage();
        }

        var key = newName.ToLowerInvariant();
        var exists = await _db.InventoryCategories.AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Name.ToLower() == key);

        if (exists)
        {
            Error = "Ya existe otra categorÃ­a con ese nombre.";
            return RedirectToPage();
        }

        c.Name = newName;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Info = "CategorÃ­a actualizada.";
        return RedirectToPage();
    }
}

