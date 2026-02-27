using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Approvals;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record PendingOrderVm(
        Guid AnchorId,
        DateTime RequestedAt,
        InventoryMovementType Type,
        string? ProjectTitle,
        string RequestedByName,
        string ResponsibleName,
        int LinesCount,
        string ItemsPreview
    );

    public List<PendingOrderVm> PendingOrders { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Traemos un bloque grande y agrupamos en memoria para construir un “pedido/orden” por lote.
        // El lote se identifica por el set de campos que ya vienen iguales cuando el empleado envía una solicitud multi-item.
        var pendingLines = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Where(m => m.Status == InventoryMovementStatus.Pending)
            .OrderByDescending(m => m.RequestedAt)
            .Take(2000)
            .ToListAsync();

        string LineLabel(InventoryMovement m)
        {
            var name = m.Item?.Name ?? "—";
            var unit = m.Item?.Unit ?? "";
            return $"{name} ({m.Quantity} {unit})";
        }

        PendingOrders = pendingLines
            .GroupBy(m => new
            {
                m.RequestedAt,
                m.RequestedByUserId,
                m.Type,
                m.ProjectId,
                m.ResponsibleUserId
            })
            .OrderByDescending(g => g.Key.RequestedAt)
            .Take(500)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.Id).First();
                var previewList = g
                    .OrderByDescending(x => x.Quantity)
                    .Take(3)
                    .Select(LineLabel)
                    .ToList();

                var preview = string.Join(", ", previewList);
                if (g.Count() > 3) preview += $" y {g.Count() - 3} más";

                return new PendingOrderVm(
                    AnchorId: first.Id,
                    RequestedAt: g.Key.RequestedAt,
                    Type: g.Key.Type,
                    ProjectTitle: first.Project?.Title,
                    RequestedByName: string.IsNullOrWhiteSpace(first.RequestedByName) ? "—" : first.RequestedByName,
                    ResponsibleName: string.IsNullOrWhiteSpace(first.ResponsibleName) ? "—" : first.ResponsibleName,
                    LinesCount: g.Count(),
                    ItemsPreview: string.IsNullOrWhiteSpace(preview) ? "—" : preview
                );
            })
            .ToList();
    }
}
