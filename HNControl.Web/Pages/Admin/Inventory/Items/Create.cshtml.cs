using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Items;

[Authorize(Policy = "InventorySupervisor")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();

    public List<SelectListItem> BrandOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public List<SelectListItem> LocationOptions { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [MaxLength(40)]
        public string? ModelCode { get; set; }

        [MaxLength(60)]
        public string? Sku { get; set; }  // opcional

        [Required(ErrorMessage = "Nombre es requerido.")]
        [MaxLength(200)]
        public string Name { get; set; } = "";

        // Se guarda texto, pero viene de catálogo
        [MaxLength(100)]
        public string Category { get; set; } = "";

        public Guid? BrandId { get; set; }

        [MaxLength(120)]
        public string? Model { get; set; }

        // Se guarda texto, pero viene de catálogo
        [MaxLength(200)]
        public string? Location { get; set; }

        [MaxLength(40)]
        public string Unit { get; set; } = "pza";

        public decimal QuantityOnHand { get; set; } = 0m; // Existencia
        public decimal ReorderLevel { get; set; } = 0m;   // Stock mínimo

        public bool IsConsumable { get; set; } = true;
        public bool IsActive { get; set; } = true;

        [MaxLength(2000)]
        public string Notes { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        await LoadCatalogsAsync();
        // defaults (si quieres)
        Input.IsActive = true;
        Input.IsConsumable = true;
        Input.Unit = string.IsNullOrWhiteSpace(Input.Unit) ? "pza" : Input.Unit;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCatalogsAsync();

        // Normaliza strings
        Input.Name = (Input.Name ?? "").Trim();
        Input.ModelCode = string.IsNullOrWhiteSpace(Input.ModelCode) ? null : Input.ModelCode.Trim().ToUpperInvariant();
        Input.Sku = string.IsNullOrWhiteSpace(Input.Sku) ? null : Input.Sku.Trim();
        Input.Category = (Input.Category ?? "").Trim();
        Input.Model = string.IsNullOrWhiteSpace(Input.Model) ? null : Input.Model.Trim();
        Input.Location = string.IsNullOrWhiteSpace(Input.Location) ? null : Input.Location.Trim();
        Input.Unit = string.IsNullOrWhiteSpace(Input.Unit) ? "pza" : Input.Unit.Trim();
        Input.Notes = (Input.Notes ?? "").Trim();

        if (!ModelState.IsValid)
        {
            Error = "Revisa los campos marcados.";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(Input.ModelCode))
        {
            var modelCodeKey = Input.ModelCode.ToLowerInvariant();
            var existsModelCode = await _db.InventoryItems
                .AsNoTracking()
                .AnyAsync(x => x.ModelCode != null && x.ModelCode.ToLower() == modelCodeKey);

            if (existsModelCode)
            {
                Error = "Ya existe un item con ese ID de modelo.";
                return Page();
            }
        }
        else
        {
            Input.ModelCode = await NextModelCodeAsync();
        }

        // (Opcional) Si SKU viene, evita duplicados por SKU
        if (!string.IsNullOrWhiteSpace(Input.Sku))
        {
            var skuKey = Input.Sku.ToLowerInvariant();
            var existsSku = await _db.InventoryItems
                .AsNoTracking()
                .AnyAsync(x => x.Sku != null && x.Sku.ToLower() == skuKey);

            if (existsSku)
            {
                Error = "Ya existe un item con ese SKU.";
                return Page();
            }
        }

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = Input.Name,
            ModelCode = Input.ModelCode,
            Sku = Input.Sku,

            Category = Input.Category,     // texto elegido desde catálogo
            BrandId = Input.BrandId,
            Model = Input.Model,
            Location = Input.Location,     // texto elegido desde catálogo

            Unit = Input.Unit,
            QuantityOnHand = Input.QuantityOnHand,
            ReorderLevel = Input.ReorderLevel,

            IsConsumable = Input.IsConsumable,
            IsActive = Input.IsActive,

            Notes = Input.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private async Task<string> NextModelCodeAsync()
    {
        var max = await _db.InventoryItems.AsNoTracking()
            .Where(x => x.ModelCode != null && x.ModelCode.StartsWith("MDL-"))
            .Select(x => x.ModelCode!)
            .ToListAsync();

        var current = 0;
        foreach (var code in max)
        {
            if (code.Length >= 8 && int.TryParse(code.AsSpan(4), out var n) && n > current)
            {
                current = n;
            }
        }

        return $"MDL-{(current + 1):D6}";
    }

    private async Task LoadCatalogsAsync()
    {
        BrandOptions = await _db.InventoryBrands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString()))
            .ToListAsync();
        BrandOptions.Insert(0, new SelectListItem("— Sin marca —", ""));

        CategoryOptions = await _db.InventoryCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Name))
            .ToListAsync();
        CategoryOptions.Insert(0, new SelectListItem("— Sin categoría —", ""));

        LocationOptions = await _db.InventoryLocations
            .AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new SelectListItem(l.Name, l.Name))
            .ToListAsync();
        LocationOptions.Insert(0, new SelectListItem("— Sin ubicación —", ""));
    }
}


