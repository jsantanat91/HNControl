using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Leaves;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public LeaveRequestStatus? StatusFilter { get; set; }

    public int PendingCount { get; set; }

    public List<LeaveRequest> Items { get; set; } = new();

    public async Task OnGetAsync(int? status = null)
    {
        StatusFilter = status.HasValue ? (LeaveRequestStatus)status.Value : null;

        PendingCount = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.Status == LeaveRequestStatus.Pending)
            .CountAsync();

        var q = _db.LeaveRequests
            .AsNoTracking()
            .Include(x => x.EmployeeProfile)
            .Include(x => x.Evidences)
            .OrderByDescending(x => x.RequestedAt)
            .AsQueryable();

        if (StatusFilter.HasValue)
            q = q.Where(x => x.Status == StatusFilter.Value);

        Items = await q.Take(500).ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var req = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null) return NotFound();

        if (req.Status != LeaveRequestStatus.Pending)
        {
            TempData["Error"] = "Solo se pueden aprobar solicitudes pendientes.";
            return RedirectToPage();
        }

        req.Status = LeaveRequestStatus.Approved;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(req.AdminComment))
            req.AdminComment = "Aprobado.";

        await _db.SaveChangesAsync();
        TempData["Success"] = "Aprobado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        var req = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null) return NotFound();

        if (req.Status != LeaveRequestStatus.Pending)
        {
            TempData["Error"] = "Solo se pueden rechazar solicitudes pendientes.";
            return RedirectToPage();
        }

        req.Status = LeaveRequestStatus.Rejected;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(req.AdminComment))
            req.AdminComment = "Rechazado.";

        await _db.SaveChangesAsync();
        TempData["Success"] = "Rechazado.";
        return RedirectToPage();
    }
}
