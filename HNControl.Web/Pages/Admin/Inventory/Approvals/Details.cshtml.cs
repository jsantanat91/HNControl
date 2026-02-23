using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Approvals;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public InventoryMovement? Movement { get; set; }

    [BindProperty]
    public DecisionInput Input { get; set; } = new();

    public class DecisionInput
    {
        [MaxLength(2000)]
        public string? AdminNote { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Movement = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (Movement == null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        if (!ModelState.IsValid) return await OnGetAsync(id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var adminName = prof?.FullName ?? (User.Identity?.Name ?? "");

        using var tx = await _db.Database.BeginTransactionAsync();

        var mov = await _db.InventoryMovements
            .Include(m => m.Item)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mov == null) return NotFound();
        if (mov.Status != InventoryMovementStatus.Pending)
            return RedirectToPage("./Index");

        if (mov.Item == null) return BadRequest();

        if (mov.Type == InventoryMovementType.Out)
        {
            if (mov.Item.QuantityOnHand < mov.Quantity)
            {
                ModelState.AddModelError(string.Empty, $"Stock insuficiente: Existencia {mov.Item.QuantityOnHand} {mov.Item.Unit}.");
                await tx.RollbackAsync();
                return await OnGetAsync(id);
            }

            mov.Item.QuantityOnHand -= mov.Quantity;
        }
        else
        {
            mov.Item.QuantityOnHand += mov.Quantity;
        }

        mov.Status = InventoryMovementStatus.Approved;
        mov.ApprovedAt = DateTime.UtcNow;
        mov.ApprovedByUserId = userId;
        mov.ApprovedByName = adminName;
        mov.AdminNote = (Input.AdminNote ?? "").Trim();

        mov.Item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        if (!ModelState.IsValid) return await OnGetAsync(id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var adminName = prof?.FullName ?? (User.Identity?.Name ?? "");

        var mov = await _db.InventoryMovements.FirstOrDefaultAsync(m => m.Id == id);
        if (mov == null) return NotFound();
        if (mov.Status != InventoryMovementStatus.Pending) return RedirectToPage("./Index");

        mov.Status = InventoryMovementStatus.Rejected;
        mov.ApprovedAt = DateTime.UtcNow;
        mov.ApprovedByUserId = userId;
        mov.ApprovedByName = adminName;
        mov.AdminNote = (Input.AdminNote ?? "").Trim();

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
