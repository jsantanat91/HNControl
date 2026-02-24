using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Leaves;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    public EmployeeProfile? Profile { get; set; }

    public int Year { get; set; }

    public int AllowanceDays { get; set; }
    public int UsedDays { get; set; }
    public int RemainingDays { get; set; }

    public List<LeaveRequest> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? year = null)
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return RedirectToPage("/Account/Login");

        Year = year ?? DateTime.Now.Year;

        Profile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (Profile == null) return Page();

        Items = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Evidences)
            .OrderByDescending(x => x.RequestedAt)
            .Take(200)
            .ToListAsync();

        AllowanceDays = Profile.VacationAllowanceDays;

        UsedDays = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Approved
                        && x.StartDate.Year == Year)
            .SumAsync(x => (int?)x.TotalDays) ?? 0;

        RemainingDays = AllowanceDays - UsedDays;
        if (RemainingDays < 0) RemainingDays = 0;

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var req = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (req == null) return NotFound();

        if (req.Status != LeaveRequestStatus.Pending)
        {
            TempData["Error"] = "Solo puedes cancelar solicitudes pendientes.";
            return RedirectToPage();
        }

        req.Status = LeaveRequestStatus.Cancelled;
        req.AdminComment = string.IsNullOrWhiteSpace(req.AdminComment)
            ? "Cancelado por el empleado."
            : req.AdminComment;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Solicitud cancelada.";
        return RedirectToPage();
    }
}
