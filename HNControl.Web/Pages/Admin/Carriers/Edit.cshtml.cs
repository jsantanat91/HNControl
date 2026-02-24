using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Carriers;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public EditModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public string? CurrentLogoUrl { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = "";

        [MaxLength(120)]
        public string ExecutiveName { get; set; } = "";

        [MaxLength(40)] public string SupportPhone { get; set; } = "";
        [MaxLength(120)] public string SupportEmail { get; set; } = "";
        [MaxLength(400)] public string SupportPortalUrl { get; set; } = "";
        [MaxLength(2000)] public string Notes { get; set; } = "";
        public bool IsActive { get; set; } = true;

        public IFormFile? LogoFile { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var c = await _db.InternetCarriers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(c.LogoStoragePath))
            CurrentLogoUrl = Url.Page("/Carriers/Logo", new { id = c.Id });

        Input = new InputModel
        {
            Id = c.Id,
            Name = c.Name,
            ExecutiveName = c.ExecutiveName,
            SupportPhone = c.SupportPhone,
            SupportEmail = c.SupportEmail,
            SupportPortalUrl = c.SupportPortalUrl,
            Notes = c.Notes,
            IsActive = c.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var c = await _db.InternetCarriers.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (c == null) return NotFound();

        c.Name = Input.Name.Trim();
        c.ExecutiveName = (Input.ExecutiveName ?? "").Trim();
        c.SupportPhone = (Input.SupportPhone ?? "").Trim();
        c.SupportEmail = (Input.SupportEmail ?? "").Trim();
        c.SupportPortalUrl = (Input.SupportPortalUrl ?? "").Trim();
        c.Notes = (Input.Notes ?? "").Trim();
        c.IsActive = Input.IsActive;
        c.UpdatedAt = DateTime.UtcNow;

        if (Input.LogoFile != null && Input.LogoFile.Length > 0)
        {
            var (path, size, contentType, original) = await _storage.SaveFileAsync(
                Input.LogoFile,
                subFolder: "carrier-logos",
                fileNameNoExt: $"carrier_{c.Id}",
                allowedExtensions: new[] { ".png", ".jpg", ".jpeg", ".webp" },
                maxBytes: 5L * 1024L * 1024L);

            c.LogoStoragePath = path;
            c.LogoSizeBytes = size;
            c.LogoContentType = contentType;
            c.LogoOriginalFileName = original;
            c.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
