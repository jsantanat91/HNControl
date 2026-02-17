using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

public class DeleteEntryModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public DeleteEntryModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
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
            .FirstOrDefaultAsync(e => e.Id == entryId && e.Week!.UserId == userId);

        if (entry == null) return NotFound();

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
            .FirstOrDefaultAsync(e => e.Id == EntryId && e.Week!.UserId == userId);

        if (entry == null) return NotFound();

        var weekId = entry.WeekId;

        _db.ViaticEntries.Remove(entry);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { id = weekId });
    }
}
