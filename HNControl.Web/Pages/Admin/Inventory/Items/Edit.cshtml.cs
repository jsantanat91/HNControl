using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Items;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required, MaxLength(160)]
        public string Name { get; set; } = "";

        [MaxLength(60)]
        public string? Sku { get; set; }

        [MaxLength(20)]
        public string Unit { get; set; } = "pz";

        public bool IsConsumable { get; set; } = true;

        [Range(0, 999999)]
        public decimal QuantityOnHand { get; set; } = 0;

        [Range(0, 999999)]
        public decimal ReorderLevel { get; set; } = 0;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var i = await _db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (i == null) return NotFound();

        Input = new InputModel
        {
            Id = i.Id,
            Name = i.Name,
            Sku = i.Sku,
            Unit = i.Unit,
            IsConsumable = i.IsConsumable,
            QuantityOnHand = i.QuantityOnHand,
            ReorderLevel = i.ReorderLevel,
            Notes = i.Notes,
            IsActive = i.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var i = await _db.InventoryItems.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (i == null) return NotFound();

        i.Name = Input.Name.Trim();
        i.Sku = (Input.Sku ?? "").Trim();
        i.Unit = (Input.Unit ?? "pz").Trim();
        i.IsConsumable = Input.IsConsumable;
        i.QuantityOnHand = Input.QuantityOnHand;
        i.ReorderLevel = Input.ReorderLevel;
        i.Notes = (Input.Notes ?? "").Trim();
        i.IsActive = Input.IsActive;
        i.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
