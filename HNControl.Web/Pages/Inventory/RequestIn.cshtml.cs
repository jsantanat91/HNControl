using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class RequestInModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public RequestInModel(ApplicationDbContext db) => _db = db;

    public List<SelectListItem> ItemOptions { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public Guid ItemId { get; set; }

        [Range(1, 999999)]
        public decimal Quantity { get; set; } = 1;

        [MaxLength(120)]
        public string? Reference { get; set; } // OC / factura / remisión

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadItemsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadItemsAsync();
        if (!ModelState.IsValid) return Page();

        var item = await _db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == Input.ItemId && i.IsActive);
        if (item == null)
        {
            ModelState.AddModelError(string.Empty, "El item no existe o está inactivo.");
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ItemId = item.Id,
            Type = InventoryMovementType.In,
            Status = InventoryMovementStatus.Pending,
            Quantity = Input.Quantity,
            Reference = (Input.Reference ?? "").Trim(),
            Notes = (Input.Notes ?? "").Trim(),
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = userId,
            RequestedByName = prof?.FullName ?? (User.Identity?.Name ?? "")
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("./MyRequests");
    }

    private async Task LoadItemsAsync()
    {
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.Sku, i.QuantityOnHand, i.Unit })
            .ToListAsync();

        ItemOptions = items.Select(i => new SelectListItem
        {
            Value = i.Id.ToString(),
            Text = $"{i.Name}{(string.IsNullOrWhiteSpace(i.Sku) ? "" : " [" + i.Sku + "]")} • OnHand: {i.QuantityOnHand} {i.Unit}"
        }).ToList();
    }
}
