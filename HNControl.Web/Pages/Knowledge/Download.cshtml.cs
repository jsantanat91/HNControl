using HNControl.Web.Data;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Knowledge;

public class DownloadModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public DownloadModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var doc = await _db.KnowledgeLinks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (doc == null) return NotFound();
        if (string.IsNullOrWhiteSpace(doc.AttachmentStoragePath)) return NotFound();

        var downloadName = string.IsNullOrWhiteSpace(doc.AttachmentOriginalFileName)
            ? $"kb_{doc.Title}.bin".Replace(' ', '_')
            : doc.AttachmentOriginalFileName;

        var (stream, contentType, name) = await _storage.OpenAsync(doc.AttachmentStoragePath, downloadName);
        return File(stream, contentType, name);
    }
}
