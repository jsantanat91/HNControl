using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class RequestDetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public RequestDetailsModel(ApplicationDbContext db) => _db = db;

    public InventoryMovement? Anchor { get; set; }
    public List<InventoryMovement> Lines { get; set; } = new();

    public string StatusLabel { get; set; } = "—";
    public string StatusCss { get; set; } = "text-bg-light";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        Anchor = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (Anchor == null) return NotFound();

        if (Anchor.RequestedByUserId != userId && Anchor.ResponsibleUserId != userId)
            return Forbid();

        Lines = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .Where(m =>
                m.RequestedAt == Anchor.RequestedAt &&
                m.RequestedByUserId == Anchor.RequestedByUserId &&
                m.Type == Anchor.Type &&
                m.ProjectId == Anchor.ProjectId &&
                m.ResponsibleUserId == Anchor.ResponsibleUserId)
            .OrderBy(m => m.Item!.Name)
            .ThenBy(m => m.Item!.Sku)
            .ToListAsync();

        var statuses = Lines.Select(x => x.Status).Distinct().ToList();
        if (statuses.Count == 1)
        {
            (StatusLabel, StatusCss) = statuses[0] switch
            {
                InventoryMovementStatus.Pending => ("Pendiente", "text-bg-warning"),
                InventoryMovementStatus.Approved => ("Aprobado", "text-bg-success"),
                InventoryMovementStatus.Rejected => ("Rechazado", "text-bg-danger"),
                _ => ("—", "text-bg-light")
            };
        }
        else
        {
            if (Lines.Any(x => x.Status == InventoryMovementStatus.Pending))
            {
                StatusLabel = "Parcial (pendiente)";
                StatusCss = "text-bg-warning";
            }
            else
            {
                StatusLabel = "Parcial";
                StatusCss = "text-bg-secondary";
            }
        }

        return Page();
    }
}
