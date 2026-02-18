using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

[Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
public class EditEntryModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public EditEntryModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    public ViaticWeek? Week { get; set; }
    public ViaticEntry? Entry { get; set; }

    [TempData] public string? Error { get; set; }
    [TempData] public string? Info { get; set; }

    [BindProperty] public Guid EntryId { get; set; }

    [BindProperty, DataType(DataType.Date)]
    public DateTime DayDate { get; set; }

    [BindProperty] public ViaticCategory Category { get; set; } = ViaticCategory.Transporte;

    [BindProperty, Required, MaxLength(300)]
    public string Description { get; set; } = "";

    [BindProperty, Range(0.01, 9999999)]
    public decimal Amount { get; set; }

    [BindProperty] public bool IsBillable { get; set; }

    [BindProperty] public IFormFile? PdfFile { get; set; }

    [BindProperty] public bool RemovePdf { get; set; }

    public SelectList CategoryItems => new(
        Enum.GetValues<ViaticCategory>().Select(x => new { Id = x, Name = x.ToString() }),
        "Id",
        "Name",
        Category
    );

    public async Task<IActionResult> OnGetAsync(Guid entryId)
    {
        var userId = _userMgr.GetUserId(User)!;

        Entry = await _db.ViaticEntries
            .Include(e => e.Attachment)
            .Include(e => e.Week)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (Entry?.Week == null) return NotFound();
        if (Entry.Week.UserId != userId) return Forbid();

        Week = Entry.Week;

        if (Week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
        {
            Error = "Semana enviada/aprobada: no se puede editar.";
            return RedirectToPage("/Viaticos/Week", new { id = Week.Id });
        }

        EntryId = Entry.Id;
        DayDate = Entry.DayDate;
        Category = Entry.Category;
        Description = Entry.Description;
        Amount = Entry.Amount;
        IsBillable = Entry.IsBillable;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        var entry = await _db.ViaticEntries
            .Include(e => e.Attachment)
            .Include(e => e.Week)
            .FirstOrDefaultAsync(e => e.Id == EntryId);

        if (entry?.Week == null) return NotFound();
        if (entry.Week.UserId != userId) return Forbid();

        var week = entry.Week;

        if (week.Status is ViaticWeekStatus.Submitted or ViaticWeekStatus.Approved)
        {
            Error = "Semana enviada/aprobada: no se puede editar.";
            return RedirectToPage("/Viaticos/Week", new { id = week.Id });
        }

        // Día dentro de semana
        var start = week.WeekStartDate.Date;
        var end = start.AddDays(6);
        if (DayDate.Date < start || DayDate.Date > end)
        {
            ModelState.AddModelError(nameof(DayDate), "Ese día no cae dentro de la semana.");
        }

        // PDF rules
        if (RemovePdf && IsBillable)
        {
            ModelState.AddModelError(nameof(RemovePdf), "No puedes borrar el PDF si el gasto es facturable.");
        }

        if (IsBillable && entry.Attachment == null && (PdfFile == null || PdfFile.Length == 0))
        {
            ModelState.AddModelError(nameof(PdfFile), "Si es facturable, el PDF es obligatorio.");
        }

        if (!ModelState.IsValid)
        {
            // Rehidratar para vista
            Week = week;
            Entry = entry;
            return Page();
        }

        entry.DayDate = DayDate.Date;
        entry.Category = Category;
        entry.Description = (Description ?? "").Trim();
        entry.Amount = Amount;
        entry.IsBillable = IsBillable;

        if (RemovePdf && entry.Attachment != null)
        {
            _db.ViaticAttachments.Remove(entry.Attachment);
            entry.Attachment = null;
        }

        if (PdfFile != null && PdfFile.Length > 0)
        {
            if (entry.Attachment == null)
            {
                entry.Attachment = new ViaticAttachment
                {
                    EntryId = entry.Id,
                    OriginalFileName = Path.GetFileName(PdfFile.FileName),
                    ContentType = "application/pdf",
                    UploadedAt = DateTime.UtcNow
                };
                _db.ViaticAttachments.Add(entry.Attachment);
            }
            else
            {
                entry.Attachment.OriginalFileName = Path.GetFileName(PdfFile.FileName);
                entry.Attachment.UploadedAt = DateTime.UtcNow;
            }

            var (path, size) = await _storage.SavePdfAsync(PdfFile, $"viaticos/{week.Id}", entry.Attachment.Id.ToString("N"));
            entry.Attachment.StoragePath = path;
            entry.Attachment.SizeBytes = size;
        }

        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await ViaticTotalsHelper.RecalcWeekAsync(_db, week.Id);
        await _db.SaveChangesAsync();

        Info = "Gasto actualizado.";
        return RedirectToPage("/Viaticos/Week", new { id = week.Id });
    }
}
