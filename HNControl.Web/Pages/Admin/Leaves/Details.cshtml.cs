using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Leaves;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db) => _db = db;

    public LeaveRequest? Item { get; set; }

    [BindProperty]
    public ReviewInput Input { get; set; } = new();

    public class ReviewInput
    {
        public Guid Id { get; set; }

        [MaxLength(600)]
        public string AdminComment { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Item = await _db.LeaveRequests
            .AsNoTracking()
            .Include(x => x.EmployeeProfile)
            .Include(x => x.Evidences)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Item == null) return NotFound();

        Input = new ReviewInput
        {
            Id = Item.Id,
            AdminComment = Item.AdminComment
        };

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync()
    {
        var req = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (req == null) return NotFound();

        if (req.Status != LeaveRequestStatus.Pending)
        {
            TempData["Error"] = "Solo se pueden aprobar solicitudes pendientes.";
            return RedirectToPage(new { id = Input.Id });
        }

        req.Status = LeaveRequestStatus.Approved;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        req.AdminComment = (Input.AdminComment ?? "").Trim();
        if (string.IsNullOrWhiteSpace(req.AdminComment)) req.AdminComment = "Aprobado.";

        await _db.SaveChangesAsync();
        TempData["Success"] = "Aprobado.";
        return RedirectToPage(new { id = Input.Id });
    }

    public async Task<IActionResult> OnPostRejectAsync()
    {
        var req = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (req == null) return NotFound();

        if (req.Status != LeaveRequestStatus.Pending)
        {
            TempData["Error"] = "Solo se pueden rechazar solicitudes pendientes.";
            return RedirectToPage(new { id = Input.Id });
        }

        req.Status = LeaveRequestStatus.Rejected;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        req.AdminComment = (Input.AdminComment ?? "").Trim();
        if (string.IsNullOrWhiteSpace(req.AdminComment)) req.AdminComment = "Rechazado.";

        await _db.SaveChangesAsync();
        TempData["Success"] = "Rechazado.";
        return RedirectToPage(new { id = Input.Id });
    }
}
