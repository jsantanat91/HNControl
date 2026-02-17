using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Viaticos;

public class AddEntryModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public AddEntryModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    [BindProperty] public Guid WeekId { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime DayDate { get; set; } = DateTime.Today;

    [BindProperty] public ViaticCategory Category { get; set; } = ViaticCategory.Transporte;

    [BindProperty, Required, MaxLength(300)]
    public string Description { get; set; } = "";

    [BindProperty, Range(0.01, 9999999)]
    public decimal Amount { get; set; }

    [BindProperty] public bool IsBillable { get; set; }

    [BindProperty] public IFormFile? PdfFile { get; set; }

    public string? Error { get; set; }

    public SelectList CategoryItems => new(
        Enum.GetValues<ViaticCategory>()
            .Select(x => new { Id = x, Name = x.ToString() }),
        "Id",
        "Name"
    );

    public async Task<IActionResult> OnGetAsync(Guid weekId, DateTime? day)
    {
        WeekId = weekId;
        DayDate = (day ?? DateTime.Today).Date;

        var userId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks.FirstOrDefaultAsync(w => w.Id == weekId && w.UserId == userId);
        if (week == null) return Forbid();

        // Solo Draft es editable
        if (week.Status != ViaticWeekStatus.Draft)
        {
            Error = "La semana ya fue enviada/validada. No se puede agregar gastos.";
            return Page();
        }

        // Asegura que el día caiga dentro de la semana
        var start = week.WeekStartDate.Date;
        var end = start.AddDays(6);
        if (DayDate.Date < start || DayDate.Date > end)
            DayDate = start;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        var week = await _db.ViaticWeeks
            .FirstOrDefaultAsync(w => w.Id == WeekId && w.UserId == userId);

        if (week == null) return Forbid();

        // Solo Draft es editable
        if (week.Status != ViaticWeekStatus.Draft)
        {
            Error = "La semana ya fue enviada/validada. No se puede agregar gastos.";
            return Page();
        }

        // Validación: el día debe estar dentro de la semana
        var start = week.WeekStartDate.Date;
        var end = start.AddDays(6);
        if (DayDate.Date < start || DayDate.Date > end)
        {
            Error = "Ese día no cae dentro de la semana.";
            return Page();
        }

        // Validación: si es facturable, PDF obligatorio
        if (IsBillable && (PdfFile == null || PdfFile.Length == 0))
        {
            Error = "Si es facturable, el PDF es obligatorio.";
            return Page();
        }

        if (!ModelState.IsValid) return Page();

        var entry = new ViaticEntry
        {
            WeekId = WeekId,
            DayDate = DayDate.Date,
            Category = Category,
            Description = (Description ?? "").Trim(),
            Amount = Amount,
            IsBillable = IsBillable,
            CreatedAt = DateTime.UtcNow
        };

        _db.ViaticEntries.Add(entry);

        // Adjuntar PDF si aplica
        if (IsBillable && PdfFile != null)
        {
            var attachment = new ViaticAttachment
            {
                EntryId = entry.Id,
                OriginalFileName = Path.GetFileName(PdfFile.FileName),
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow
            };

            // guardamos con ID único
            var (path, size) = await _storage.SavePdfAsync(PdfFile, $"viaticos/{week.Id}", attachment.Id.ToString("N"));
            attachment.StoragePath = path;
            attachment.SizeBytes = size;

            _db.ViaticAttachments.Add(attachment);
        }

        week.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Viaticos/Week", new { id = WeekId });
    }
}
