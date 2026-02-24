using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Admin.Carriers;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public CreateModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
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

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var c = new InternetCarrier
        {
            Name = Input.Name.Trim(),
            ExecutiveName = (Input.ExecutiveName ?? "").Trim(),
            SupportPhone = (Input.SupportPhone ?? "").Trim(),
            SupportEmail = (Input.SupportEmail ?? "").Trim(),
            SupportPortalUrl = (Input.SupportPortalUrl ?? "").Trim(),
            Notes = (Input.Notes ?? "").Trim(),
            IsActive = Input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.InternetCarriers.Add(c);
        await _db.SaveChangesAsync();

        // Logo (opcional)
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
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("./Index");
    }
}
