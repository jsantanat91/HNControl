using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class MyRequestsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public MyRequestsModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string Filter { get; set; } = "all"; // all|pending|approved|rejected|mixed

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

    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int MixedCount { get; set; }

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
            .Take(3000)
            .ToListAsync();

        string lineLabel(InventoryMovement m)
        {
            var name = m.Item?.Name ?? "-";
            var unit = m.Item?.Unit ?? string.Empty;
            return $"{name} ({m.Quantity} {unit})";
        }

        (string label, string css) statusBadge(List<InventoryMovement> orderLines)
        {
            var statuses = orderLines.Select(x => x.Status).Distinct().ToList();
            if (statuses.Count == 1)
            {
                return statuses[0] switch
                {
                    InventoryMovementStatus.Pending => ("Pendiente", "hn-badge-amber"),
                    InventoryMovementStatus.Approved => ("Aprobada", "hn-badge-green"),
                    InventoryMovementStatus.Rejected => ("Rechazada", "hn-badge-red"),
                    _ => ("Pendiente", "hn-badge-slate")
                };
            }

            return ("En proceso", "hn-badge-blue");
        }

        var grouped = lines
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
                var list = g.ToList();
                var first = list.OrderBy(x => x.Id).First();
                var previewList = list.OrderByDescending(x => x.Quantity).Take(3).Select(lineLabel).ToList();
                var preview = string.Join(", ", previewList);
                if (list.Count > 3) preview += $" y {list.Count - 3} mas";

                var (label, css) = statusBadge(list);

                return new OrderRowVm(
                    AnchorId: first.Id,
                    RequestedAt: g.Key.RequestedAt,
                    Type: g.Key.Type,
                    ProjectTitle: first.Project?.Title,
                    ResponsibleName: string.IsNullOrWhiteSpace(first.ResponsibleName) ? "-" : first.ResponsibleName,
                    StatusLabel: label,
                    StatusCss: css,
                    LinesCount: list.Count,
                    ItemsPreview: string.IsNullOrWhiteSpace(preview) ? "-" : preview
                );
            })
            .ToList();

        PendingCount = grouped.Count(x => x.StatusLabel == "Pendiente");
        ApprovedCount = grouped.Count(x => x.StatusLabel == "Aprobada");
        RejectedCount = grouped.Count(x => x.StatusLabel == "Rechazada");
        MixedCount = grouped.Count(x => x.StatusLabel == "En proceso");

        var f = (Filter ?? "all").Trim().ToLowerInvariant();
        Orders = f switch
        {
            "pending" => grouped.Where(x => x.StatusLabel == "Pendiente").ToList(),
            "approved" => grouped.Where(x => x.StatusLabel == "Aprobada").ToList(),
            "rejected" => grouped.Where(x => x.StatusLabel == "Rechazada").ToList(),
            "mixed" => grouped.Where(x => x.StatusLabel == "En proceso").ToList(),
            _ => grouped
        };
    }
}
