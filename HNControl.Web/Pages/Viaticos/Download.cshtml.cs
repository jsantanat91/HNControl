using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

[Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
public class DownloadModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public DownloadModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid attachmentId)
    {
        var att = await _db.ViaticAttachments
            .Include(a => a.Entry!)
            .ThenInclude(e => e.Week!)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (att?.Entry?.Week == null) return NotFound();

        var userId = _userMgr.GetUserId(User)!;
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var isOwner = att.Entry.Week.UserId == userId;

        if (!isAdmin && !isOwner) return Forbid();

        var downloadName = string.IsNullOrWhiteSpace(att.OriginalFileName) ? "factura.pdf" : att.OriginalFileName;
        var (stream, contentType, _) = await _storage.OpenAsync(att.StoragePath, downloadName);

        return File(stream, contentType, downloadName);
    }
}
