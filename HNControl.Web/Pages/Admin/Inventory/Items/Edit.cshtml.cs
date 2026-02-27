using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Items;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();

    public List<SelectListItem> BrandOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public List<SelectListItem> LocationOptions { get; set; } = new();

    public string? Error { get; set; }

    public record MovementVm(
        Guid Id,
        DateTime RequestedAt,
        InventoryMovementType Type,
        decimal Quantity,
        string Unit,
        InventoryMovementStatus Status,
        string RequestedByName,
        string ResponsibleName,
        string? ApprovedByName,
        DateTime? ApprovedAt
    );

    public List<MovementVm> RecentMovements { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

        [MaxLength(60)]
        public string? Sku { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public string Category { get; set; } = "";

        public Guid? BrandId { get; set; }

        [MaxLength(120)]
        public string? Model { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        [MaxLength(40)]
        public string Unit { get; set; } = "pza";

        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }

        public bool IsConsumable { get; set; }
        public bool IsActive { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var item = await _db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        Input = new InputModel
        {
            Id = item.Id,
            Name = item.Name,
            Sku = item.Sku,
            Category = item.Category ?? "",
            BrandId = item.BrandId,
            Model = item.Model,
            Location = item.Location,
            Unit = item.Unit,
            QuantityOnHand = item.QuantityOnHand,
            ReorderLevel = item.ReorderLevel,
            IsConsumable = item.IsConsumable,
            IsActive = item.IsActive,
            Notes = item.Notes ?? ""
        };

        await LoadCatalogsAsync();
        await LoadRecentMovementsAsync(item.Id, item.Unit);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCatalogsAsync();

        Input.Name = (Input.Name ?? "").Trim();
        Input.Sku = string.IsNullOrWhiteSpace(Input.Sku) ? null : Input.Sku.Trim();
        Input.Category = (Input.Category ?? "").Trim();
        Input.Model = string.IsNullOrWhiteSpace(Input.Model) ? null : Input.Model.Trim();
        Input.Location = string.IsNullOrWhiteSpace(Input.Location) ? null : Input.Location.Trim();
        Input.Unit = string.IsNullOrWhiteSpace(Input.Unit) ? "pza" : Input.Unit.Trim();
        Input.Notes = (Input.Notes ?? "").Trim();

        if (!ModelState.IsValid)
        {
            Error = "Revisa los campos marcados.";
            await LoadRecentMovementsAsync(Input.Id, Input.Unit);
            return Page();
        }

        var item = await _db.InventoryItems.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (item == null) return NotFound();

        item.Name = Input.Name;
        item.Sku = Input.Sku;
        item.Category = Input.Category;
        item.BrandId = Input.BrandId;
        item.Model = Input.Model;
        item.Location = Input.Location;
        item.Unit = Input.Unit;

        item.QuantityOnHand = Input.QuantityOnHand;
        item.ReorderLevel = Input.ReorderLevel;

        item.IsConsumable = Input.IsConsumable;
        item.IsActive = Input.IsActive;

        item.Notes = Input.Notes;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
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

    private async Task LoadRecentMovementsAsync(Guid itemId, string unit)
    {
        RecentMovements = await _db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.ItemId == itemId)
            .OrderByDescending(m => m.RequestedAt)
            .Take(30)
            .Select(m => new MovementVm(
                m.Id,
                m.RequestedAt,
                m.Type,
                m.Quantity,
                unit,
                m.Status,
                m.RequestedByName,
                m.ResponsibleName,
                m.ApprovedByName,
                m.ApprovedAt
            ))
            .ToListAsync();
    }
}
