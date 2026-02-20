using System.Security.Claims;
using System.Text.RegularExpressions;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.ServiceOrders;

[Authorize]
public class WorkModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _cfg;

    public WorkModel(ApplicationDbContext db, IFileStorage storage, IConfiguration cfg)
    {
        _db = db;
        _storage = storage;
        _cfg = cfg;
    }

    public ServiceOrder? Order { get; set; }
    public string? Info { get; set; }

    public string ClientDownloadUrl { get; set; } = "";

    public string TechName { get; set; } = "";
    public string ClientSignerName { get; set; } = "";

    public bool HasTechSignature { get; set; }
    public bool HasClientSignature { get; set; }

    public string ChecklistCompletionPercent { get; set; } = "0%";

    public class ItemVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public bool IsDone { get; set; }
        public string Notes { get; set; } = "";
    }

    public class EvidenceVm
    {
        public Guid Id { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string UploadedAtLocal { get; set; } = "";
    }

    [BindProperty] public List<ItemVm> ItemsPost { get; set; } = new();
    [BindProperty] public IFormFile? EvidenceFile { get; set; }

    public List<EvidenceVm> Evidences { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var ok = await LoadAsync(id);
        return ok ? Page() : Forbid();
    }

    public async Task<IActionResult> OnPostSaveChecklistAsync(Guid id)
    {
        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();

        foreach (var vm in ItemsPost)
        {
            var it = Order.Checklist.FirstOrDefault(x => x.Id == vm.Id);
            if (it == null) continue;

            it.IsDone = vm.IsDone;
            it.Notes = (vm.Notes ?? "").Trim();
        }

        if (Order.Status == ServiceOrderStatus.Created)
        {
            Order.Status = ServiceOrderStatus.InProgress;
            Order.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        Info = "Checklist guardado.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadEvidenceAsync(Guid id)
    {
        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();

        if (EvidenceFile == null || EvidenceFile.Length == 0)
        {
            Info = "Selecciona un archivo.";
            await LoadAsync(id);
            return Page();
        }

        var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".heic", ".heif" };
        var maxBytes = (_cfg.GetValue<int?>("Storage:MaxEvidenceMb") ?? 25) * 1024L * 1024L;

        var (path, size, contentType, originalName) = await _storage.SaveFileAsync(
            EvidenceFile,
            $"serviceorders/{Order.Id}/evidence",
            Guid.NewGuid().ToString("N"),
            allowed,
            maxBytes
        );

        _db.ServiceOrderEvidences.Add(new ServiceOrderEvidence
        {
            OrderId = Order.Id,
            OriginalFileName = originalName,
            ContentType = contentType,
            SizeBytes = size,
            StoragePath = path,
            UploadedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        Info = "Evidencia subida.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadEvidenceAsync(Guid evidenceId)
    {
        var ev = await _db.ServiceOrderEvidences.FirstOrDefaultAsync(x => x.Id == evidenceId);
        if (ev == null) return NotFound();

        var order = await _db.ServiceOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ev.OrderId);
        if (order == null) return NotFound();

        if (!IsAdmin() && GetUserId() != order.AssignedUserId)
            return Forbid();

        var (stream, contentType, originalName) = await _storage.OpenAsync(ev.StoragePath, ev.OriginalFileName);
        return File(stream, contentType, originalName);
    }

    // ✅ Un botón: firma y envía
    public async Task<IActionResult> OnPostSignAndSubmitAsync(
        Guid id,
        string? TechName,
        string? ClientName,
        string? TechSigDataUrl,
        string? ClientSigDataUrl)
    {
        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();

        await UpsertSignatureIfPresentAsync(id, SignatureRole.Technician, TechName, TechSigDataUrl);
        await UpsertSignatureIfPresentAsync(id, SignatureRole.Client, ClientName, ClientSigDataUrl);

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        if (Order == null) return NotFound();

        if (!HasTechSignature || !HasClientSignature)
        {
            Info = "Para enviar a revisión se requieren ambas firmas (técnico y cliente).";
            return Page();
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ServiceOrders""
SET ""Status"" = {ServiceOrderStatus.InReview},
    ""SubmittedForReviewAt"" = {DateTime.UtcNow}
WHERE ""Id"" = {id};
");

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        Info = "✅ Firmas guardadas y enviado a revisión.";
        return Page();
    }

    // Compatibilidad: handlers viejos (ya sin EF SaveChanges para firmas)
    public async Task<IActionResult> OnPostSaveSignaturesAsync(Guid id, string? TechName, string? ClientName, string? TechSigDataUrl, string? ClientSigDataUrl)
    {
        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();

        await UpsertSignatureIfPresentAsync(id, SignatureRole.Technician, TechName, TechSigDataUrl);
        await UpsertSignatureIfPresentAsync(id, SignatureRole.Client, ClientName, ClientSigDataUrl);

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        Info = "Firmas guardadas.";
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitForReviewAsync(Guid id, string? TechName, string? ClientName, string? TechSigDataUrl, string? ClientSigDataUrl)
    {
        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();

        await UpsertSignatureIfPresentAsync(id, SignatureRole.Technician, TechName, TechSigDataUrl);
        await UpsertSignatureIfPresentAsync(id, SignatureRole.Client, ClientName, ClientSigDataUrl);

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        if (Order == null) return NotFound();

        if (!HasTechSignature || !HasClientSignature)
        {
            Info = "Para enviar a revisión se requieren ambas firmas (técnico y cliente).";
            return Page();
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ServiceOrders""
SET ""Status"" = {ServiceOrderStatus.InReview},
    ""SubmittedForReviewAt"" = {DateTime.UtcNow}
WHERE ""Id"" = {id};
");

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        Info = "Enviado a revisión. El admin podrá aprobar/rechazar y generar el PDF.";
        return Page();
    }

    public async Task<IActionResult> OnGetSignatureImageAsync(Guid id, string role)
    {
        var order = await _db.ServiceOrders
            .Include(o => o.Signatures)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();
        if (!IsAdmin() && GetUserId() != order.AssignedUserId) return Forbid();

        var r = role.Equals("Client", StringComparison.OrdinalIgnoreCase)
            ? SignatureRole.Client
            : SignatureRole.Technician;

        var sig = order.Signatures.FirstOrDefault(s => s.Role == r);
        if (sig == null || string.IsNullOrWhiteSpace(sig.StoragePath)) return NotFound();

        var (stream, contentType, _) = await _storage.OpenAsync(sig.StoragePath, "signature.png");
        return File(stream, contentType);
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        Order = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Checklist)
            .Include(o => o.Evidences)
            .Include(o => o.Signatures)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (Order == null) return true;

        if (!IsAdmin() && GetUserId() != Order.AssignedUserId)
            return false;

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(Order.PublicToken))
            ClientDownloadUrl = $"{baseUrl}/Public/ServiceOrder/{Order.PublicToken}";

        ItemsPost = Order.Checklist
            .OrderBy(i => i.SortOrder)
            .Select(i => new ItemVm
            {
                Id = i.Id,
                Title = i.Title,
                IsDone = i.IsDone,
                Notes = i.Notes
            }).ToList();

        ChecklistCompletionPercent = $"{(GetChecklistCompletion(Order) * 100m):0.#}%";

        Evidences = Order.Evidences
            .OrderByDescending(e => e.UploadedAt)
            .Select(e => new EvidenceVm
            {
                Id = e.Id,
                OriginalFileName = e.OriginalFileName,
                UploadedAtLocal = e.UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            }).ToList();

        var tech = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);
        var cli = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Client);

        TechName = tech?.SignedByName ?? "";
        ClientSignerName = cli?.SignedByName ?? "";

        HasTechSignature = tech != null && !string.IsNullOrWhiteSpace(tech.StoragePath);
        HasClientSignature = cli != null && !string.IsNullOrWhiteSpace(cli.StoragePath);

        return true;
    }

    private decimal GetChecklistCompletion(ServiceOrder order)
    {
        if (order.Checklist == null || order.Checklist.Count == 0) return 0m;
        var done = order.Checklist.Count(x => x.IsDone);
        return (decimal)done / order.Checklist.Count;
    }

    // ✅ SQL delete+insert = cero drama con concurrencia
    private async Task<bool> UpsertSignatureIfPresentAsync(Guid orderId, SignatureRole role, string? name, string? dataUrl)
    {
        name = (name ?? "").Trim();

        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image/png;base64,"))
            return false;

        var base64 = Regex.Replace(dataUrl, "^data:image\\/png;base64,", "");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch { return false; }

        if (bytes.Length < 1200) return false;

        var fileName = $"{role.ToString().ToLower()}_{Guid.NewGuid():N}.png";
        var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{orderId}/signatures", fileName, "image/png");

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM ""ServiceOrderSignatures""
WHERE ""OrderId"" = {orderId} AND ""Role"" = {role};

INSERT INTO ""ServiceOrderSignatures""
    (""Id"", ""OrderId"", ""Role"", ""SignedByName"", ""StoragePath"", ""SignedAt"")
VALUES
    ({Guid.NewGuid()}, {orderId}, {role}, {name}, {path}, {DateTime.UtcNow});
");

        return true;
    }

    private bool IsAdmin() => User.IsInRole(AppRoles.Admin);

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}