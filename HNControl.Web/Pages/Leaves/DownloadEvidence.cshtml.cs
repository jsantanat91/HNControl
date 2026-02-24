using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Leaves;

[Authorize(Policy = "EmployeeOnly")]
public class DownloadEvidenceModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public DownloadEvidenceModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var ev = await _db.LeaveEvidences
            .AsNoTracking()
            .Include(e => e.LeaveRequest)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ev == null) return NotFound();

        var userId = _userMgr.GetUserId(User);
        var isAdmin = User.IsInRole(AppRoles.Admin);

        if (!isAdmin && (string.IsNullOrWhiteSpace(userId) || ev.LeaveRequest?.UserId != userId))
            return Forbid();

        var (stream, contentType, downloadName) = await _storage.OpenAsync(ev.StoragePath, ev.OriginalFileName);
        return File(stream, contentType, downloadName);
    }
}
