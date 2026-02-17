using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

public class ServiceOrderModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public ServiceOrderModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public string Token { get; set; } = "";
    public ServiceOrder? Order { get; set; }
    public string ClientName { get; set; } = "";
    public string? Info { get; set; }

    public List<ItemVm> Items { get; set; } = new();
    public List<string> EvidenceNames { get; set; } = new();

    public string TechName { get; set; } = "";
    public string ClientSignerName { get; set; } = "";

    public class ItemVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public bool IsDone { get; set; }
        public string Notes { get; set; } = "";
    }

    [BindProperty] public List<ItemVm> ItemsPost { get; set; } = new();

    [BindProperty] public IFormFile? EvidenceFile { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        Token = token;
        await LoadAsync(token);
        return Order == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostSaveChecklistAsync(string token)
    {
        await LoadAsync(token);
        if (Order == null) return NotFound();

        // Actualiza checklist
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
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadEvidenceAsync(string token)
    {
        await LoadAsync(token);
        if (Order == null) return NotFound();

        if (EvidenceFile == null || EvidenceFile.Length == 0)
        {
            Info = "Selecciona un archivo.";
            await LoadAsync(token);
            return Page();
        }

        var allowed = new[] { ".png", ".jpg", ".jpeg", ".pdf" };
        var (path, size, contentType, originalName) = await _storage.SaveFileAsync(
            EvidenceFile,
            $"serviceorders/{Order.Id}/evidence",
            Guid.NewGuid().ToString("N"),
            allowed,
            25 * 1024 * 1024
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
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveSignaturesAsync(string token, string? TechName, string? ClientName, string? TechSigDataUrl, string? ClientSigDataUrl)
    {
        await LoadAsync(token);
        if (Order == null) return NotFound();

        await SaveSignatureIfPresent(Order, SignatureRole.Technician, TechName, TechSigDataUrl);
        await SaveSignatureIfPresent(Order, SignatureRole.Client, ClientName, ClientSigDataUrl);

        await _db.SaveChangesAsync();

        Info = "Firmas guardadas.";
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitForReviewAsync(string token, string? TechName, string? ClientName, string? TechSigDataUrl, string? ClientSigDataUrl)
    {
        await LoadAsync(token);
        if (Order == null) return NotFound();

        await SaveSignatureIfPresent(Order, SignatureRole.Technician, TechName, TechSigDataUrl);
        await SaveSignatureIfPresent(Order, SignatureRole.Client, ClientName, ClientSigDataUrl);

        var tech = Order.Signatures.Any(s => s.Role == SignatureRole.Technician);
        var client = Order.Signatures.Any(s => s.Role == SignatureRole.Client);

        if (!tech || !client)
        {
            Info = "Para enviar a revisión se requieren ambas firmas (técnico y cliente).";
            await LoadAsync(token);
            return Page();
        }

        Order.Status = ServiceOrderStatus.InReview;
        await _db.SaveChangesAsync();

        Info = "Enviado a revisión. El admin generará el PDF y lo enviará por correo.";
        await LoadAsync(token);
        return Page();
    }

    private async Task LoadAsync(string token)
    {
        Token = token;

        Order = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Checklist)
            .Include(o => o.Evidences)
            .Include(o => o.Signatures)
            .FirstOrDefaultAsync(o => o.PublicToken == token);

        Items.Clear();
        EvidenceNames.Clear();

        if (Order == null) return;

        ClientName = Order.Client?.Name ?? "";

        Items = Order.Checklist
            .OrderBy(i => i.SortOrder)
            .Select(i => new ItemVm { Id = i.Id, Title = i.Title, IsDone = i.IsDone, Notes = i.Notes })
            .ToList();

        // Para el POST binder
        ItemsPost = Items.Select(x => new ItemVm { Id = x.Id, Title = x.Title, IsDone = x.IsDone, Notes = x.Notes }).ToList();

        EvidenceNames = Order.Evidences.OrderByDescending(e => e.UploadedAt).Select(e => e.OriginalFileName).ToList();

        var tech = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);
        var cli = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Client);

        TechName = tech?.SignedByName ?? "";
        ClientSignerName = cli?.SignedByName ?? "";
    }

    private async Task SaveSignatureIfPresent(ServiceOrder order, SignatureRole role, string? name, string? dataUrl)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image/png;base64,")) return;

        var base64 = Regex.Replace(dataUrl, "^data:image\\/png;base64,", "");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch { return; }

        var fileName = $"{role.ToString().ToLower()}_{Guid.NewGuid():N}.png";
        var (path, _, _) = await _storage.SaveBytesAsync(bytes, $"serviceorders/{order.Id}/signatures", fileName, "image/png");

        // reemplaza firma si ya existía (última gana)
        var existing = order.Signatures.FirstOrDefault(s => s.Role == role);
        if (existing != null)
        {
            existing.SignedByName = name;
            existing.StoragePath = path;
            existing.SignedAt = DateTime.UtcNow;
        }
        else
        {
            order.Signatures.Add(new ServiceOrderSignature
            {
                OrderId = order.Id,
                Role = role,
                SignedByName = name,
                StoragePath = path,
                SignedAt = DateTime.UtcNow
            });
        }
    }
}
