using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Admin.Inventory.Items;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
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

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        _db.InventoryItems.Add(new InventoryItem
        {
            Name = Input.Name.Trim(),
            Sku = (Input.Sku ?? "").Trim(),
            Unit = (Input.Unit ?? "pz").Trim(),
            IsConsumable = Input.IsConsumable,
            QuantityOnHand = Input.QuantityOnHand,
            ReorderLevel = Input.ReorderLevel,
            Notes = (Input.Notes ?? "").Trim(),
            IsActive = Input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
