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

    public InventoryMovement? Anchor { get; set; }
    public List<InventoryMovement> Lines { get; set; } = new();

    public bool CanDecide { get; set; }
    public bool HasMixedStatuses { get; set; }

    [BindProperty]
    public DecisionInput Input { get; set; } = new();

    public class DecisionInput
    {
        [MaxLength(2000)]
        public string? AdminNote { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Anchor = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (Anchor == null) return NotFound();

        Lines = await LoadOrderLinesAsync(Anchor);

        var statuses = Lines.Select(x => x.Status).Distinct().ToList();
        HasMixedStatuses = statuses.Count > 1;
        CanDecide = Lines.Count > 0 && Lines.All(x => x.Status == InventoryMovementStatus.Pending);

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        if (!ModelState.IsValid) return await OnGetAsync(id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var adminName = prof?.FullName ?? (User.Identity?.Name ?? "");

        // 1) ancla (sin tracking)
        var anchor = await _db.InventoryMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (anchor == null) return NotFound();

        using var tx = await _db.Database.BeginTransactionAsync();

        // 2) cargar líneas con tracking
        var lines = await LoadOrderLinesTrackedAsync(anchor);

        if (lines.Count == 0) return NotFound();

        if (lines.Any(x => x.Status != InventoryMovementStatus.Pending))
        {
            ModelState.AddModelError(string.Empty, "Esta orden ya fue procesada (total o parcialmente). Para mantener consistencia, no se puede aprobar por orden.");
            await tx.RollbackAsync();
            return await OnGetAsync(id);
        }

        // 3) validación y actualización de stock
        if (lines.First().Type == InventoryMovementType.Out)
        {
            var byItem = lines.GroupBy(x => x.ItemId)
                .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var g in byItem)
            {
                var anyLine = lines.FirstOrDefault(x => x.ItemId == g.ItemId);
                var item = anyLine?.Item;
                if (item == null) continue;

                if (item.QuantityOnHand < g.Qty)
                {
                    ModelState.AddModelError(string.Empty, $"Stock insuficiente para '{item.Name}': Existencia {item.QuantityOnHand} {item.Unit}, requerido {g.Qty}.");
                    await tx.RollbackAsync();
                    return await OnGetAsync(id);
                }
            }

            foreach (var g in byItem)
            {
                var item = lines.First(x => x.ItemId == g.ItemId).Item!;
                item.QuantityOnHand -= g.Qty;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            var byItem = lines.GroupBy(x => x.ItemId)
                .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var g in byItem)
            {
                var item = lines.First(x => x.ItemId == g.ItemId).Item;
                if (item == null) continue;
                item.QuantityOnHand += g.Qty;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 4) aprobar líneas
        var note = (Input.AdminNote ?? "").Trim();
        var now = DateTime.UtcNow;

        foreach (var mov in lines)
        {
            mov.Status = InventoryMovementStatus.Approved;
            mov.ApprovedAt = now;
            mov.ApprovedByUserId = userId;
            mov.ApprovedByName = adminName;
            mov.AdminNote = note;
        }

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

        var anchor = await _db.InventoryMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (anchor == null) return NotFound();

        var lines = await LoadOrderLinesTrackedAsync(anchor);
        if (lines.Count == 0) return NotFound();

        if (lines.Any(x => x.Status != InventoryMovementStatus.Pending))
        {
            ModelState.AddModelError(string.Empty, "Esta orden ya fue procesada (total o parcialmente). Para mantener consistencia, no se puede rechazar por orden.");
            return await OnGetAsync(id);
        }

        var note = (Input.AdminNote ?? "").Trim();
        var now = DateTime.UtcNow;

        foreach (var mov in lines)
        {
            mov.Status = InventoryMovementStatus.Rejected;
            mov.ApprovedAt = now;
            mov.ApprovedByUserId = userId;
            mov.ApprovedByName = adminName;
            mov.AdminNote = note;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }

    private async Task<List<InventoryMovement>> LoadOrderLinesAsync(InventoryMovement anchor)
    {
        return await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .Where(m =>
                m.RequestedAt == anchor.RequestedAt &&
                m.RequestedByUserId == anchor.RequestedByUserId &&
                m.Type == anchor.Type &&
                m.ProjectId == anchor.ProjectId &&
                m.ResponsibleUserId == anchor.ResponsibleUserId)
            .OrderBy(m => m.Item!.Name)
            .ThenBy(m => m.Item!.Sku)
            .ToListAsync();
    }

    private async Task<List<InventoryMovement>> LoadOrderLinesTrackedAsync(InventoryMovement anchor)
    {
        return await _db.InventoryMovements
            .Include(m => m.Item)
            .Where(m =>
                m.RequestedAt == anchor.RequestedAt &&
                m.RequestedByUserId == anchor.RequestedByUserId &&
                m.Type == anchor.Type &&
                m.ProjectId == anchor.ProjectId &&
                m.ResponsibleUserId == anchor.ResponsibleUserId)
            .ToListAsync();
    }
}
