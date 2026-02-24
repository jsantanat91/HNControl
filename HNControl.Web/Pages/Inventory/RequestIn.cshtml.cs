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

    public class LineInput
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

    public class InputModel
    {
        [MaxLength(2000)]
        public string? Notes { get; set; }

        public List<LineInput> Lines { get; set; } = new() { new LineInput() };
    }

    public async Task OnGetAsync()
    {
        await LoadItemsAsync();
        if (Input.Lines == null || Input.Lines.Count == 0)
            Input.Lines = new List<LineInput> { new LineInput() };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadItemsAsync();

        var lines = (Input.Lines ?? new List<LineInput>())
            .Where(l => l.ItemId != Guid.Empty && l.Quantity > 0)
            .ToList();

        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Agrega al menos un item.");

        if (!ModelState.IsValid) return Page();

        var ids = lines.Select(x => x.ItemId).Distinct().ToList();
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.IsActive)
            .Select(i => i.Id)
            .ToListAsync();

        var set = items.ToHashSet();
        if (set.Count != ids.Count)
        {
            ModelState.AddModelError(string.Empty, "Uno o más items no existen o están inactivos.");
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

        var now = DateTime.UtcNow;
        var globalNotes = (Input.Notes ?? "").Trim();

        foreach (var l in lines)
        {
            var lineNotes = (l.Notes ?? "").Trim();
            var notes = string.Join("\n", new[] { globalNotes, lineNotes }.Where(x => !string.IsNullOrWhiteSpace(x)));

            _db.InventoryMovements.Add(new InventoryMovement
            {
                ItemId = l.ItemId,
                Type = InventoryMovementType.In,
                Status = InventoryMovementStatus.Pending,
                Quantity = l.Quantity,
                Reference = (l.Reference ?? "").Trim(),
                Notes = notes,
                RequestedAt = now,
                RequestedByUserId = userId,
                RequestedByName = prof?.FullName ?? (User.Identity?.Name ?? "")
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("./MyRequests");
    }

    private async Task LoadItemsAsync()
    {
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.Sku, i.Category, i.Location, i.QuantityOnHand, i.Unit })
            .ToListAsync();

        ItemOptions = items.Select(i => new SelectListItem
        {
            Value = i.Id.ToString(),
            Text = $"{i.Name}{(string.IsNullOrWhiteSpace(i.Sku) ? "" : " [" + i.Sku + "]")}" +
                   $" • {(string.IsNullOrWhiteSpace(i.Category) ? "Sin categoría" : i.Category)}" +
                   $" • {(string.IsNullOrWhiteSpace(i.Location) ? "—" : i.Location)}" +
                   $" • Existencia: {i.QuantityOnHand} {i.Unit}"
        }).ToList();
    }
}
