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

    public WorkModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public ServiceOrder? Order { get; set; }
    public string? Info { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public bool IsReadOnly { get; set; }
    public bool IsClaimedByCurrentUser { get; set; }
    public bool CanTakeOwnership { get; set; }

    public string TechName { get; set; } = "";
    public bool HasTechSignature { get; set; }

    public string ChecklistCompletionPercent { get; set; } = "0%";
    public string TotalChecklistCompletionPercent { get; set; } = "0%";
    [BindProperty] public string AreaNotes { get; set; } = "";

    public List<ServiceOrderWorkItem> WorkItems { get; set; } = new();

    public List<ServiceOrderWorkflowArea> WorkflowAreas { get; set; } = Enum.GetValues<ServiceOrderWorkflowArea>().OrderBy(x => (int)x).ToList();

    public class WorkItemPostVm
    {
        public Guid Id { get; set; }
        public string WorkPerformed { get; set; } = "";
        public string MaterialsUsed { get; set; } = "";
        public string TechnicianNotes { get; set; } = "";
        public bool IsCompleted { get; set; }
    }

    public class ChecklistGroupVm
    {
        public Guid? WorkItemId { get; set; }
        public string Title { get; set; } = "";
        public string TypeLabel { get; set; } = "";
        public int? WorkItemPostIndex { get; set; }
        public List<int> ItemIndices { get; set; } = new();
    }

    public List<ChecklistGroupVm> ChecklistGroups { get; set; } = new();

    [BindProperty] public List<WorkItemPostVm> WorkItemsPost { get; set; } = new();

    public class ItemVm
    {
        public Guid Id { get; set; }
        public Guid? WorkItemId { get; set; }
        public string Category { get; set; } = "";
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
        id = ResolveId(id);
        var ok = await LoadAsync(id);
        Info ??= TempData["Info"] as string;
        return ok ? Page() : Forbid();
    }

    public async Task<IActionResult> OnPostTakeAsync(Guid id)
    {
        id = ResolveId(id);
        if (id == Guid.Empty) return NotFound();

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        if (order.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed)
        {
            TempData["Info"] = "La orden ya no acepta edicion.";
            return RedirectToPage(new { id });
        }

        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(order.ClaimedByUserId) && order.ClaimedByUserId != userId)
        {
            TempData["Info"] = "La orden ya fue tomada por otro técnico. Pide al admin desasignarla.";
            return RedirectToPage(new { id });
        }

        order.ClaimedByUserId = userId;
        order.ClaimedAt = DateTime.UtcNow;

        if (order.Status == ServiceOrderStatus.Created)
        {
            order.Status = ServiceOrderStatus.InProgress;
            order.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Info"] = "Orden tomada."
            ;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSaveChecklistAsync(Guid id)
    {
        id = ResolveId(id);
        if (id == Guid.Empty) return NotFound();

        Order = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Checklist)
            .Include(o => o.WorkItems)
            .Include(o => o.AssignedEmployee)
            .Include(o => o.ClaimedByEmployee)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (Order == null) return NotFound();
        if (!CanEditOrder(Order)) return Forbid();

        if (Order.CurrentArea != ServiceOrderWorkflowArea.Ejecucion)
            return Forbid();

        foreach (var vm in ItemsPost ?? new())
        {
            var it = Order.Checklist.FirstOrDefault(x => x.Id == vm.Id);
            if (it == null) continue;

            it.IsDone = vm.IsDone;
            it.Notes = (vm.Notes ?? "").Trim();
        }

        if (Order.WorkItems != null && Order.WorkItems.Count > 0 && WorkItemsPost != null && WorkItemsPost.Count > 0)
        {
            foreach (var wvm in WorkItemsPost)
            {
                var wi = Order.WorkItems.FirstOrDefault(x => x.Id == wvm.Id);
                if (wi == null) continue;

                wi.WorkPerformed = (wvm.WorkPerformed ?? "").Trim();
                wi.MaterialsUsed = (wvm.MaterialsUsed ?? "").Trim();
                wi.TechnicianNotes = (wvm.TechnicianNotes ?? "").Trim();
                wi.IsCompleted = wvm.IsCompleted;
                wi.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (Order.Status == ServiceOrderStatus.Created)
        {
            Order.Status = ServiceOrderStatus.InProgress;
            Order.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        TempData["Info"] = "Guardado.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSaveAreaNotesAsync(Guid id)
    {
        id = ResolveId(id);
        if (id == Guid.Empty) return NotFound();

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (!CanEditOrder(order)) return Forbid();

        var notes = (AreaNotes ?? string.Empty).Trim();
        if (notes.Length > 4000)
            notes = notes[..4000];

        switch (order.CurrentArea)
        {
            case ServiceOrderWorkflowArea.Levantamiento:
                order.LevantamientoNotes = notes;
                break;
            case ServiceOrderWorkflowArea.Materiales:
                order.MaterialesNotes = notes;
                break;
            default:
                return Forbid();
        }

        if (order.Status == ServiceOrderStatus.Created)
        {
            order.Status = ServiceOrderStatus.InProgress;
            order.StartedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Info"] = "Observaciones guardadas.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUploadEvidenceAsync(Guid id)
    {
        id = ResolveId(id);
        if (id == Guid.Empty) return NotFound();

        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();
        if (!CanEditOrder(Order)) return Forbid();

        if (EvidenceFile == null || EvidenceFile.Length == 0)
        {
            Info = "Selecciona un archivo.";
            await LoadAsync(id);
            return Page();
        }

        var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".heic", ".heif" };
        var maxBytes = 25 * 1024L * 1024L;

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

        var (stream, contentType, originalName) = await _storage.OpenAsync(ev.StoragePath, ev.OriginalFileName);
        return File(stream, contentType, originalName);
    }

    public async Task<IActionResult> OnPostNextAreaAsync(Guid id)
    {
        id = ResolveId(id);
        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (!CanEditOrder(order)) return Forbid();

        var current = (int)order.CurrentArea;
        var max = WorkflowAreas.Max(x => (int)x);
        if (current < max)
            order.CurrentArea = (ServiceOrderWorkflowArea)(current + 1);

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPreviousAreaAsync(Guid id)
    {
        id = ResolveId(id);
        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (!CanEditOrder(order)) return Forbid();

        var current = (int)order.CurrentArea;
        var min = WorkflowAreas.Min(x => (int)x);
        if (current > min)
            order.CurrentArea = (ServiceOrderWorkflowArea)(current - 1);

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSignAndSubmitAsync(Guid id, string? TechSigDataUrl)
    {
        id = ResolveId(id);
        if (id == Guid.Empty) return NotFound();

        var ok = await LoadAsync(id);
        if (!ok || Order == null) return Forbid();
        if (!CanEditOrder(Order)) return Forbid();

        if (Order.CurrentArea != ServiceOrderWorkflowArea.CierreTecnico)
        {
            Info = "Para enviar la orden debes llegar al area final (Cierre tecnico).";
            return Page();
        }

        var techName = await GetCurrentUserDisplayNameAsync();
        await UpsertSignatureIfPresentAsync(id, SignatureRole.Technician, techName, TechSigDataUrl);

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        if (Order == null) return NotFound();

        if (!HasTechSignature)
        {
            Info = "Se requiere la firma del tecnico para enviar a revision.";
            return Page();
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ServiceOrders""
SET ""Status"" = {ServiceOrderStatus.InReview},
    ""SubmittedForReviewAt"" = {DateTime.UtcNow},
    ""PdfStoragePath"" = NULL,
    ""PdfGeneratedAt"" = NULL
WHERE ""Id"" = {id};
");

        _db.ChangeTracker.Clear();
        await LoadAsync(id);

        Info = "Firmada y enviada a revision.";
        return Page();
    }

    public async Task<IActionResult> OnGetSignatureImageAsync(Guid id, string role)
    {
        var order = await _db.ServiceOrders
            .Include(o => o.Signatures)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

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
            .Include(o => o.WorkItems)
            .Include(o => o.AssignedEmployee)
            .Include(o => o.ClaimedByEmployee)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (Order == null) return true;

        var added = false;
        WorkItems = Order.WorkItems.OrderBy(w => w.SortOrder).ToList();

        if (Order.Type != ServiceOrderType.Global && Order.Checklist.Count == 0)
            added |= await EnsureChecklistFromTemplateAsync(Order, Order.Type, workItemId: null);

        foreach (var w in WorkItems)
        {
            if (!Order.Checklist.Any(x => x.WorkItemId == w.Id))
                added |= await EnsureChecklistFromTemplateAsync(Order, w.Type, workItemId: w.Id);
        }

        if (added)
            await _db.SaveChangesAsync();

        WorkItemsPost = WorkItems.Select(w => new WorkItemPostVm
        {
            Id = w.Id,
            WorkPerformed = w.WorkPerformed,
            MaterialsUsed = w.MaterialsUsed,
            TechnicianNotes = w.TechnicianNotes,
            IsCompleted = w.IsCompleted
        }).ToList();

        var flat = new List<ItemVm>();
        var groups = new List<ChecklistGroupVm>();

        void AddGroup(Guid? workItemId, string title, string typeLabel, int? wiPostIndex, IEnumerable<ServiceOrderChecklistItem> items)
        {
            var g = new ChecklistGroupVm
            {
                WorkItemId = workItemId,
                Title = title,
                TypeLabel = typeLabel,
                WorkItemPostIndex = wiPostIndex
            };

            foreach (var it in items.OrderBy(x => x.SortOrder))
            {
                flat.Add(new ItemVm
                {
                    Id = it.Id,
                    WorkItemId = it.WorkItemId,
                    Category = it.Category,
                    Title = it.Title,
                    IsDone = it.IsDone,
                    Notes = it.Notes
                });
                g.ItemIndices.Add(flat.Count - 1);
            }

            if (g.ItemIndices.Count > 0)
                groups.Add(g);
        }

        var checklistForCurrentArea = Order.Checklist
            .Where(i => ResolveAreaFromCategory(i.Category) == Order.CurrentArea)
            .ToList();

        if (WorkItems.Count == 0)
        {
            AddGroup(null, "Checklist", Order.Type.GetDisplayName(), null, checklistForCurrentArea);
        }
        else
        {
            AddGroup(
                null,
                "Checklist general",
                "General",
                null,
                checklistForCurrentArea.Where(i => i.WorkItemId == null));

            for (var wiIndex = 0; wiIndex < WorkItems.Count; wiIndex++)
            {
                var w = WorkItems[wiIndex];
                AddGroup(
                    w.Id,
                    w.Title,
                    w.Type.GetDisplayName(),
                    wiIndex,
                    checklistForCurrentArea.Where(i => i.WorkItemId == w.Id));
            }
        }

        ItemsPost = flat;
        ChecklistGroups = groups;

        ChecklistCompletionPercent = $"{(GetChecklistCompletion(checklistForCurrentArea) * 100m):0.#}%";
        TotalChecklistCompletionPercent = $"{(GetChecklistCompletion(Order.Checklist) * 100m):0.#}%";

        Evidences = Order.Evidences
            .OrderByDescending(e => e.UploadedAt)
            .Select(e => new EvidenceVm
            {
                Id = e.Id,
                OriginalFileName = e.OriginalFileName,
                UploadedAtLocal = e.UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            }).ToList();

        var tech = Order.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);

        TechName = !string.IsNullOrWhiteSpace(tech?.SignedByName)
            ? tech!.SignedByName
            : await GetCurrentUserDisplayNameAsync();

        HasTechSignature = tech != null && !string.IsNullOrWhiteSpace(tech.StoragePath);

        AreaNotes = Order.CurrentArea switch
        {
            ServiceOrderWorkflowArea.Levantamiento => Order.LevantamientoNotes ?? string.Empty,
            ServiceOrderWorkflowArea.Materiales => Order.MaterialesNotes ?? string.Empty,
            _ => string.Empty
        };

        IsClaimedByCurrentUser = Order.ClaimedByUserId == GetUserId();
        IsReadOnly = !IsAdmin() && !IsClaimedByCurrentUser;
        var closed = Order.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.Finalized or ServiceOrderStatus.Completed;
        CanTakeOwnership = !IsAdmin() && !closed && (string.IsNullOrWhiteSpace(Order.ClaimedByUserId) || IsClaimedByCurrentUser);

        return true;
    }

    private decimal GetChecklistCompletion(IReadOnlyCollection<ServiceOrderChecklistItem> checklist)
    {
        if (checklist == null || checklist.Count == 0) return 0m;
        var done = checklist.Count(x => x.IsDone);
        return (decimal)done / checklist.Count;
    }

    private static ServiceOrderWorkflowArea ResolveAreaFromCategory(string? category)
    {
        var c = (category ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(c))
            return ServiceOrderWorkflowArea.Ejecucion;

        if (c.Contains("levant") || c.Contains("diagnost"))
            return ServiceOrderWorkflowArea.Levantamiento;

        if (c.Contains("material"))
            return ServiceOrderWorkflowArea.Materiales;

        if (c.Contains("prueb") || c.Contains("cierre") || c.Contains("entrega") || c.Contains("validac") || c.Contains("recomend"))
            return ServiceOrderWorkflowArea.CierreTecnico;

        if (c.Contains("manten") || c.Contains("ejec") || c.Contains("instal") || c.Contains("cable") || c.Contains("equipo") || c.Contains("red"))
            return ServiceOrderWorkflowArea.Ejecucion;

        return ServiceOrderWorkflowArea.Ejecucion;
    }

    private async Task<bool> EnsureChecklistFromTemplateAsync(ServiceOrder order, ServiceOrderType type, Guid? workItemId)
    {
        if (order.Checklist.Any(x => x.WorkItemId == workItemId))
            return false;

        var template = await _db.ServiceOrderChecklistTemplates
            .Include(t => t.Items)
            .Where(t => t.IsActive && t.Type == type)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync();

        if (template == null || template.Items.Count == 0)
            return false;

        foreach (var it in template.Items.OrderBy(x => x.SortOrder))
        {
            order.Checklist.Add(new ServiceOrderChecklistItem
            {
                OrderId = order.Id,
                WorkItemId = workItemId,
                SortOrder = it.SortOrder,
                Category = it.Category,
                Title = it.Title,
                IsRequired = it.IsRequired,
                IsDone = false,
                Notes = ""
            });
        }

        return true;
    }

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

    private bool IsAdmin() => AppRoles.IsGlobalAdmin(User);

    private bool CanEditOrder(ServiceOrder order)
    {
        if (IsAdmin()) return true;
        var userId = GetUserId();
        return !string.IsNullOrWhiteSpace(userId) && order.ClaimedByUserId == userId;
    }

    private Guid ResolveId(Guid id)
    {
        if (id != Guid.Empty) return id;
        if (Id != Guid.Empty) return Id;

        if (RouteData.Values.TryGetValue("id", out var v) && Guid.TryParse(v?.ToString(), out var rid))
            return rid;

        var formId = Request?.Form["id"].ToString();
        if (!string.IsNullOrWhiteSpace(formId) && Guid.TryParse(formId, out var fid))
            return fid;

        return Guid.Empty;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private async Task<string> GetCurrentUserDisplayNameAsync()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return User.Identity?.Name ?? "Tecnico";

        var fullName = await _db.EmployeeProfiles
            .Where(e => e.UserId == userId)
            .Select(e => e.FullName)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName;

        return User.Identity?.Name ?? "Tecnico";
    }
}
