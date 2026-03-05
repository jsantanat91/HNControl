using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Knowledge;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ISecretProtector _protector;

    public EditModel(ApplicationDbContext db, IFileStorage storage, ISecretProtector protector)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public IFormFile? Attachment { get; set; }
    [BindProperty] public bool RemoveAttachment { get; set; }

    public IReadOnlyList<string> CategoryOptions => KnowledgeCatalog.Categories;
    public bool HasAttachment { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }
        [Required, MaxLength(200)] public string Title { get; set; } = "";
        [Required, MaxLength(100)] public string Category { get; set; } = "General";
        [Required] public KnowledgeDocType DocType { get; set; } = KnowledgeDocType.ManualInterno;
        [Required] public KnowledgeStatus Status { get; set; } = KnowledgeStatus.Publicado;

        [MaxLength(600)] public string Url { get; set; } = "";
        [MaxLength(600)] public string Description { get; set; } = "";
        [MaxLength(8000)] public string Body { get; set; } = "";
        [MaxLength(500)] public string Tags { get; set; } = "";

        [MaxLength(200)] public string OwnerName { get; set; } = "";
        [MaxLength(200)] public string ReviewerName { get; set; } = "";
        public DateTime? ReviewDueAt { get; set; }
        public bool IsPinned { get; set; }

        [MaxLength(160)] public string AccessUsername { get; set; } = "";
        [MaxLength(300)] public string AccessSecret { get; set; } = "";
        [MaxLength(1200)] public string AccessNotes { get; set; } = "";

        public int Version { get; set; }
        public string ExistingAttachmentName { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var doc = await _db.KnowledgeLinks.FirstOrDefaultAsync(x => x.Id == id);
        if (doc == null) return NotFound();

        Input = new InputModel
        {
            Id = doc.Id,
            Title = doc.Title,
            Category = doc.Category,
            DocType = doc.DocType,
            Status = doc.Status,
            Url = doc.Url,
            Description = doc.Description,
            Body = doc.Body,
            Tags = doc.Tags,
            OwnerName = doc.OwnerName,
            ReviewerName = doc.ReviewerName,
            ReviewDueAt = doc.ReviewDueAt,
            IsPinned = doc.IsPinned,
            AccessUsername = doc.AccessUsername,
            AccessSecret = _protector.Unprotect(doc.AccessSecretProtected),
            AccessNotes = doc.AccessNotes,
            Version = doc.Version,
            ExistingAttachmentName = doc.AttachmentOriginalFileName
        };

        HasAttachment = !string.IsNullOrWhiteSpace(doc.AttachmentStoragePath);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var doc = await _db.KnowledgeLinks.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (doc == null) return NotFound();

        doc.Title = (Input.Title ?? "").Trim();
        doc.Category = (Input.Category ?? "General").Trim();
        doc.DocType = Input.DocType;
        doc.Status = Input.Status;
        doc.Url = (Input.Url ?? "").Trim();
        doc.Description = (Input.Description ?? "").Trim();
        doc.Body = (Input.Body ?? "").Trim();
        doc.Tags = NormalizeTags(Input.Tags);
        doc.OwnerName = (Input.OwnerName ?? "").Trim();
        doc.ReviewerName = (Input.ReviewerName ?? "").Trim();
        doc.ReviewDueAt = Input.ReviewDueAt;
        doc.IsPinned = Input.IsPinned;
        doc.AccessUsername = (Input.AccessUsername ?? "").Trim();
        doc.AccessSecretProtected = _protector.Protect((Input.AccessSecret ?? "").Trim());
        doc.AccessNotes = (Input.AccessNotes ?? "").Trim();

        if (doc.Status == KnowledgeStatus.Publicado && doc.PublishedAt == null)
            doc.PublishedAt = DateTime.UtcNow;

        if (RemoveAttachment && !string.IsNullOrWhiteSpace(doc.AttachmentStoragePath))
        {
            await _storage.DeleteIfExistsAsync(doc.AttachmentStoragePath);
            doc.AttachmentStoragePath = "";
            doc.AttachmentOriginalFileName = "";
            doc.AttachmentContentType = "";
            doc.AttachmentSizeBytes = null;
        }

        if (Attachment != null)
        {
            if (!string.IsNullOrWhiteSpace(doc.AttachmentStoragePath))
                await _storage.DeleteIfExistsAsync(doc.AttachmentStoragePath);

            var stored = await _storage.SaveFileAsync(
                Attachment,
                subFolder: "knowledge/files",
                fileNameNoExt: $"kb_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}",
                allowedExtensions: new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".png", ".jpg", ".jpeg", ".webp" },
                maxBytes: 25 * 1024 * 1024);

            doc.AttachmentStoragePath = stored.storagePath;
            doc.AttachmentSizeBytes = stored.sizeBytes;
            doc.AttachmentContentType = stored.contentType;
            doc.AttachmentOriginalFileName = stored.originalName;
        }

        doc.Version = (doc.Version <= 0 ? 1 : doc.Version) + 1;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedByName = await ResolveCurrentUserNameAsync();
        doc.OwnerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? doc.OwnerUserId;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Knowledge/Details", new { id = doc.Id });
    }

    private static string NormalizeTags(string tags)
    {
        var list = (tags ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct()
            .Take(20);

        return string.Join(",", list);
    }

    private async Task<string> ResolveCurrentUserNameAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId)) return User.Identity?.Name ?? "admin";

        var profileName = await _db.EmployeeProfiles
            .Where(x => x.UserId == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(profileName))
            return profileName;

        return User.Identity?.Name ?? "admin";
    }
}
