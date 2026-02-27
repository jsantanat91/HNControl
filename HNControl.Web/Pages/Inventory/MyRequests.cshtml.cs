using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class MyRequestsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public MyRequestsModel(ApplicationDbContext db) => _db = db;

    public record OrderRowVm(
        Guid AnchorId,
        DateTime RequestedAt,
        InventoryMovementType Type,
        string? ProjectTitle,
        string ResponsibleName,
        string StatusLabel,
        string StatusCss,
        int LinesCount,
        string ItemsPreview
    );

    public List<OrderRowVm> Orders { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var lines = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .Where(m => m.RequestedByUserId == userId || m.ResponsibleUserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .Take(2000)
            .ToListAsync();

        string LineLabel(InventoryMovement m)
        {
            var name = m.Item?.Name ?? "—";
            var unit = m.Item?.Unit ?? "";
            return $"{name} ({m.Quantity} {unit})";
        }

        (string label, string css) StatusBadge(IEnumerable<InventoryMovement> g)
        {
            var statuses = g.Select(x => x.Status).Distinct().ToList();
            if (statuses.Count == 1)
            {
                return statuses[0] switch
                {
                    InventoryMovementStatus.Pending => ("Pendiente", "text-bg-warning"),
                    InventoryMovementStatus.Approved => ("Aprobado", "text-bg-success"),
                    InventoryMovementStatus.Rejected => ("Rechazado", "text-bg-danger"),
                    _ => ("—", "text-bg-light")
                };
            }

            // mixto (histórico)
            if (g.Any(x => x.Status == InventoryMovementStatus.Pending)) return ("Parcial (pendiente)", "text-bg-warning");
            return ("Parcial", "text-bg-secondary");
        }

        Orders = lines
            .GroupBy(m => new
            {
                m.RequestedAt,
                m.RequestedByUserId,
                m.Type,
                m.ProjectId,
                m.ResponsibleUserId
            })
            .OrderByDescending(g => g.Key.RequestedAt)
            .Take(300)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.Id).First();
                var previewList = g.OrderByDescending(x => x.Quantity).Take(3).Select(LineLabel).ToList();
                var preview = string.Join(", ", previewList);
                if (g.Count() > 3) preview += $" y {g.Count() - 3} más";

                var (label, css) = StatusBadge(g);

                return new OrderRowVm(
                    AnchorId: first.Id,
                    RequestedAt: g.Key.RequestedAt,
                    Type: g.Key.Type,
                    ProjectTitle: first.Project?.Title,
                    ResponsibleName: string.IsNullOrWhiteSpace(first.ResponsibleName) ? "—" : first.ResponsibleName,
                    StatusLabel: label,
                    StatusCss: css,
                    LinesCount: g.Count(),
                    ItemsPreview: string.IsNullOrWhiteSpace(preview) ? "—" : preview
                );
            })
            .ToList();
    }
}
