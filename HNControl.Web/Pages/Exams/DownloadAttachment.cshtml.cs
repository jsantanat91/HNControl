using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Exams;

[Authorize]
public class DownloadAttachmentModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public DownloadAttachmentModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var att = await _db.ExamAnswerAttachments
            .AsNoTracking()
            .Include(x => x.Answer)
                .ThenInclude(a => a!.Assignment)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (att == null) return NotFound();

        var userId = _userMgr.GetUserId(User);
        var isAdmin = User.IsInRole(AppRoles.Admin);

        if (!isAdmin && (string.IsNullOrWhiteSpace(userId) || att.Answer?.Assignment?.UserId != userId))
            return Forbid();

        var (stream, contentType, downloadName) = await _storage.OpenAsync(att.StoragePath, att.OriginalFileName);
        return File(stream, contentType, downloadName);
    }
}
