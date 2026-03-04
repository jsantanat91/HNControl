using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.ServiceOrders;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public string? Info { get; set; }

    public record Row(
        Guid Id,
        string Client,
        string Title,
        string Type,
        string Status,
        string Area,
        string ClaimedBy,
        string Created,
        string Due,
        bool CanTake,
        bool IsMine,
        bool HasPdf);

    public List<Row> Rows { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.IsInRole(AppRoles.Admin))
            return Redirect("/Admin/ServiceOrders/Index");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var orders = await _db.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Include(o => o.ClaimedByEmployee)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        Rows = orders.Select(o =>
        {
            var closed = o.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed;
            var isMine = o.ClaimedByUserId == userId;

            return new Row(
                o.Id,
                o.Client?.Name ?? "-",
                o.Title,
                o.Type.GetDisplayName(),
                o.Status.GetDisplayName(),
                o.CurrentArea.GetDisplayName(),
                o.ClaimedByEmployee?.FullName ?? "Sin tomar",
                o.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                o.EstimatedEndDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-",
                !closed,
                isMine,
                !string.IsNullOrWhiteSpace(o.PdfStoragePath)
            );
        }).ToList();

        Info = TempData["Info"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostTakeAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        if (order.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
        {
            TempData["Info"] = "La orden ya no acepta edicion.";
            return RedirectToPage();
        }

        order.ClaimedByUserId = userId;
        order.ClaimedAt = DateTime.UtcNow;

        if (order.Status == ServiceOrderStatus.Created)
        {
            order.Status = ServiceOrderStatus.InProgress;
            order.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        TempData["Info"] = "Orden tomada. Ya puedes editarla.";
        return RedirectToPage("/ServiceOrders/Work", new { id = order.Id });
    }
}
