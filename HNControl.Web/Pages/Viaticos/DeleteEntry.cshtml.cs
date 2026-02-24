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
public class DeleteEntryModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public DeleteEntryModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    [BindProperty(SupportsGet = true)] public Guid EntryId { get; set; }

    public Guid WeekId { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid entryId)
    {
        EntryId = entryId;

        var userId = _userMgr.GetUserId(User)!;
        var entry = await _db.ViaticEntries
            .Include(e => e.Week!)
            .Include(e => e.Attachment)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.Week!.UserId == userId);

        if (entry == null) return NotFound();

        if (entry.Week!.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
            return Forbid();

        WeekId = entry.WeekId;
        Description = entry.Description;
        Amount = entry.Amount;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        var entry = await _db.ViaticEntries
            .Include(e => e.Week!)
            .Include(e => e.Attachment)
            .FirstOrDefaultAsync(e => e.Id == EntryId && e.Week!.UserId == userId);

        if (entry == null) return NotFound();

        if (entry.Week!.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
            return Forbid();

        var weekId = entry.WeekId;

        if (entry.Attachment != null)
            await _storage.DeleteIfExistsAsync(entry.Attachment.StoragePath);

        _db.ViaticEntries.Remove(entry);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { id = weekId });
    }
}
