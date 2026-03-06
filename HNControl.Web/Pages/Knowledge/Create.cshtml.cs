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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ISecretProtector _protector;

    public CreateModel(ApplicationDbContext db, IFileStorage storage, ISecretProtector protector)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public IFormFile? Attachment { get; set; }

    public IReadOnlyList<string> CategoryOptions => KnowledgeCatalog.Categories;

    public class InputModel
    {
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
    }

    public async Task OnGetAsync()
    {
        Input.OwnerName = await ResolveCurrentUserNameAsync();

        if (string.IsNullOrWhiteSpace(Input.Category))
            Input.Category = "General";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        Input.Title = (Input.Title ?? "").Trim();
        Input.Category = (Input.Category ?? "General").Trim();
        Input.Url = (Input.Url ?? "").Trim();
        Input.Description = (Input.Description ?? "").Trim();
        Input.Body = (Input.Body ?? "").Trim();
        Input.Tags = NormalizeTags(Input.Tags);
        Input.OwnerName = (Input.OwnerName ?? "").Trim();
        Input.ReviewerName = (Input.ReviewerName ?? "").Trim();
        Input.AccessUsername = (Input.AccessUsername ?? "").Trim();
        Input.AccessNotes = (Input.AccessNotes ?? "").Trim();

        if (string.IsNullOrWhiteSpace(Input.Url) && string.IsNullOrWhiteSpace(Input.Body) && Attachment == null)
        {
            ModelState.AddModelError(string.Empty, "Captura al menos un recurso: URL, contenido o archivo adjunto.");
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var userName = await ResolveCurrentUserNameAsync();

        try
        {
            var entity = new KnowledgeLink
            {
                Title = Input.Title,
                Category = string.IsNullOrWhiteSpace(Input.Category) ? "General" : Input.Category,
                DocType = Input.DocType,
                Status = Input.Status,
                Url = Input.Url,
                Description = Input.Description,
                Body = Input.Body,
                Tags = Input.Tags,
                OwnerName = userName,
                OwnerUserId = userId,
                ReviewerName = Input.ReviewerName,
                ReviewDueAt = Input.ReviewDueAt,
                IsPinned = Input.IsPinned,
                AccessUsername = Input.AccessUsername,
                AccessSecretProtected = _protector.Protect(Input.AccessSecret),
                AccessNotes = Input.AccessNotes,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByName = userName,
                PublishedAt = Input.Status == KnowledgeStatus.Publicado ? DateTime.UtcNow : null
            };

            if (Attachment != null)
            {
                var stored = await _storage.SaveFileAsync(
                    Attachment,
                    subFolder: "knowledge/files",
                    fileNameNoExt: $"kb_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}",
                    allowedExtensions: new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".png", ".jpg", ".jpeg", ".webp" },
                    maxBytes: 25 * 1024 * 1024);

                entity.AttachmentStoragePath = stored.storagePath;
                entity.AttachmentSizeBytes = stored.sizeBytes;
                entity.AttachmentContentType = stored.contentType;
                entity.AttachmentOriginalFileName = stored.originalName;
            }

            _db.KnowledgeLinks.Add(entity);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Knowledge/Details", new { id = entity.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"No se pudo guardar el documento: {ex.Message}");
            return Page();
        }
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
        if (string.IsNullOrWhiteSpace(userId)) return "Administrador";

        var profileName = await _db.EmployeeProfiles
            .Where(x => x.UserId == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(profileName))
            return profileName;

        return User.Identity?.Name ?? "Administrador";
    }
}
