using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Approvals;

[Authorize(Policy = "InventorySupervisor")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string Filter { get; set; } = "pending"; // all|pending|approved|rejected|inprocess

    public record OrderVm(
        Guid AnchorId,
        DateTime RequestedAt,
        DateTime LastUpdatedAt,
        InventoryMovementType Type,
        string? ProjectTitle,
        string RequestedByName,
        string ResponsibleName,
        int LinesCount,
        string ItemsPreview,
        string StatusLabel,
        string StatusCss
    );

    public List<OrderVm> Orders { get; set; } = new();

    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int InProcessCount { get; set; }

    public async Task OnGetAsync()
    {
        var allLines = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .OrderByDescending(m => m.RequestedAt)
            .Take(5000)
            .ToListAsync();

        string itemLabel(InventoryMovement m)
        {
            var item = m.Item?.Name ?? "-";
            var unit = m.Item?.Unit ?? string.Empty;
            return $"{item} ({m.Quantity} {unit})";
        }

        (string label, string css) statusOf(List<InventoryMovement> lines)
        {
            var set = lines.Select(x => x.Status).Distinct().ToList();

            if (set.Count == 1)
            {
                return set[0] switch
                {
                    InventoryMovementStatus.Pending => ("Pendiente", "hn-badge-amber"),
                    InventoryMovementStatus.Approved => ("Aprobada", "hn-badge-green"),
                    InventoryMovementStatus.Rejected => ("Rechazada", "hn-badge-red"),
                    _ => ("Pendiente", "hn-badge-slate")
                };
            }

            return ("En proceso", "hn-badge-blue");
        }

        var grouped = allLines
            .GroupBy(m => new
            {
                m.RequestedAt,
                m.RequestedByUserId,
                m.Type,
                m.ProjectId,
                m.ResponsibleUserId
            })
            .Select(g =>
            {
                var first = g.OrderBy(x => x.Id).First();
                var list = g.ToList();
                var (statusLabel, statusCss) = statusOf(list);
                var previewList = list.OrderByDescending(x => x.Quantity).Take(3).Select(itemLabel).ToList();
                var preview = string.Join(", ", previewList);
                if (list.Count > 3) preview += $" y {list.Count - 3} mas";

                var lastUpdated = list.Max(x => x.ApprovedAt ?? x.RequestedAt);

                return new OrderVm(
                    AnchorId: first.Id,
                    RequestedAt: g.Key.RequestedAt,
                    LastUpdatedAt: lastUpdated,
                    Type: g.Key.Type,
                    ProjectTitle: first.Project?.Title,
                    RequestedByName: string.IsNullOrWhiteSpace(first.RequestedByName) ? "-" : first.RequestedByName,
                    ResponsibleName: string.IsNullOrWhiteSpace(first.ResponsibleName) ? "-" : first.ResponsibleName,
                    LinesCount: list.Count,
                    ItemsPreview: string.IsNullOrWhiteSpace(preview) ? "-" : preview,
                    StatusLabel: statusLabel,
                    StatusCss: statusCss
                );
            })
            .OrderByDescending(x => x.RequestedAt)
            .ToList();

        PendingCount = grouped.Count(x => x.StatusLabel == "Pendiente");
        ApprovedCount = grouped.Count(x => x.StatusLabel == "Aprobada");
        RejectedCount = grouped.Count(x => x.StatusLabel == "Rechazada");
        InProcessCount = grouped.Count(x => x.StatusLabel == "En proceso");

        var f = (Filter ?? "pending").Trim().ToLowerInvariant();
        Orders = f switch
        {
            "all" => grouped,
            "approved" => grouped.Where(x => x.StatusLabel == "Aprobada").ToList(),
            "rejected" => grouped.Where(x => x.StatusLabel == "Rechazada").ToList(),
            "inprocess" => grouped.Where(x => x.StatusLabel == "En proceso").ToList(),
            _ => grouped.Where(x => x.StatusLabel == "Pendiente").ToList()
        };
    }
}
