using System.ComponentModel.DataAnnotations;
using System.Linq;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList TypeItems { get; set; } = default!;
    public SelectList WorkTypeItems { get; set; } = default!;
    public SelectList StatusItems { get; set; } = default!;
    public SelectList ProjectItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;
    public List<ServiceOrderFormData.ContractOption> ContractOptions { get; set; } = new();

    [BindProperty] public InputModel? Input { get; set; }

    public List<ServiceOrderWorkItem> WorkItems { get; set; } = new();

    [BindProperty] public NewWorkItemInput NewWorkItem { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required] public ServiceOrderType Type { get; set; }

        [Required] public ServiceOrderStatus Status { get; set; }

        [Required] public Guid ClientId { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? ClientServiceContractId { get; set; }

        public string? AssignedUserId { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime ExpectedEndDate { get; set; }

        public string Description { get; set; } = "";
    }

    public class NewWorkItemInput
    {
        [Required]
        public ServiceOrderType Type { get; set; } = ServiceOrderType.Preventivo;

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [MaxLength(2000)]
        public string Description { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var order = await _db.ServiceOrders
            .Include(o => o.WorkItems)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (order == null) return NotFound();

        Input = new InputModel
        {
            Id = order.Id,
            Title = order.Title,
            Type = order.Type,
            Status = order.Status,
            ClientId = order.ClientId,
            ProjectId = order.ProjectId,
            ClientServiceContractId = order.ClientServiceContractId,
            AssignedUserId = order.AssignedUserId,
            StartDate = ServiceOrderFormData.EditableDate(order.StartedAt ?? order.CreatedAt),
            ExpectedEndDate = ServiceOrderFormData.EditableDate(order.EstimatedEndDate ?? (order.StartedAt ?? order.CreatedAt).AddDays(2)),
            Description = order.Description ?? ""
        };

        WorkItems = order.WorkItems.OrderBy(w => w.SortOrder).ToList();
        NewWorkItem.Type = order.Type == ServiceOrderType.Global ? ServiceOrderType.Preventivo : order.Type;

        await LoadListsAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        if (Input == null) return NotFound();
        WorkItems = await _db.ServiceOrderWorkItems.Where(w => w.OrderId == Input.Id).OrderBy(w => w.SortOrder).ToListAsync();
        NewWorkItem.Type = Input.Type == ServiceOrderType.Global ? ServiceOrderType.Preventivo : Input.Type;

        // Exclude the activity form while preserving binding errors from the main form.
        foreach (var key in ModelState.Keys.Where(key => key == nameof(NewWorkItem) || key.StartsWith(nameof(NewWorkItem) + ".")).ToList())
            ModelState.Remove(key);
        TryValidateModel(Input, nameof(Input));

        if (!ModelState.IsValid)
        {
            Error = "No se pudo guardar. Revisa los campos señalados.";
            return Page();
        }

        Error = await ServiceOrderFormData.ValidateAsync(_db, Input.ClientId, Input.ProjectId,
            Input.ClientServiceContractId, Input.StartDate, Input.ExpectedEndDate);
        if (Error != null) return Page();

        var order = await _db.ServiceOrders
            .Include(o => o.WorkItems)
            .FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (order == null) return NotFound();

        order.Title = (Input.Title ?? "").Trim();

        // Si ya es Global (tiene actividades), no permitimos â€œbajarâ€ el tipo.
        order.Type = (order.WorkItems.Count > 0 || order.Type == ServiceOrderType.Global)
            ? ServiceOrderType.Global
            : Input.Type;

        order.Status = Input.Status;

        order.ClientId = Input.ClientId;
        order.ProjectId = Input.ProjectId;
        order.ClientServiceContractId = Input.ClientServiceContractId;

        order.AssignedUserId = string.IsNullOrWhiteSpace(Input.AssignedUserId) ? null : Input.AssignedUserId.Trim();

        order.StartedAt = TimeUtil.UtcDate(Input.StartDate);
        order.EstimatedEndDate = TimeUtil.UtcDate(Input.ExpectedEndDate);
        order.Description = (Input.Description ?? "").Trim();

        await _db.SaveChangesAsync();
        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

    public async Task<IActionResult> OnPostConvertToGlobalAsync(Guid id)
    {
        // âœ… VersiÃ³n "quirÃºrgica" (sin grafo trackeado) para evitar DbUpdateConcurrencyException.
        var order = await _db.ServiceOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new { o.Id, o.Type, o.Title, o.Description })
            .FirstOrDefaultAsync();

        if (order == null) return NotFound();

        var hasWorkItems = await _db.ServiceOrderWorkItems.AsNoTracking().AnyAsync(w => w.OrderId == id);
        if (order.Type == ServiceOrderType.Global || hasWorkItems)
        {
            // Si ya hay actividades, fuerza el tipo a Global.
            if (hasWorkItems && order.Type != ServiceOrderType.Global)
            {
                await _db.ServiceOrders
                    .Where(o => o.Id == id && o.Type != ServiceOrderType.Global)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Type, ServiceOrderType.Global));
            }
            return RedirectToPage(new { id });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var first = new ServiceOrderWorkItem
            {
                OrderId = id,
                SortOrder = 0,
                Type = order.Type,
                Title = string.IsNullOrWhiteSpace(order.Title) ? "Actividad 1" : order.Title,
                Description = order.Description ?? "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ServiceOrderWorkItems.Add(first);
            await _db.SaveChangesAsync();

            // mover checklist general (WorkItemId NULL) a esta actividad
            await _db.ServiceOrderChecklistItems
                .Where(x => x.OrderId == id && x.WorkItemId == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.WorkItemId, first.Id));

            // si no habÃ­a checklist, crear desde plantilla
            await EnsureChecklistFromTemplateDbAsync(id, first.Type, first.Id);
            await _db.SaveChangesAsync();

            // marcar la orden como global
            await _db.ServiceOrders
                .Where(o => o.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Type, ServiceOrderType.Global));

            await tx.CommitAsync();
            return RedirectToPage(new { id });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IActionResult> OnPostAddWorkItemAsync(Guid id)
    {
        // IMPORTANTÃSIMO:
        // Al postear desde el form de actividades, el route-param "id" puede ligar a Input.Id y disparar validaciÃ³n
        // de otros campos requeridos (Title/Status/etc) y entonces parece que el botÃ³n â€œno hace nadaâ€.
        // AquÃ­ validamos SOLO NewWorkItem.
        ModelState.Clear();
        TryValidateModel(NewWorkItem, nameof(NewWorkItem));
        if (!ModelState.IsValid)
            return await ReloadAndShowAsync(id);

        var order = await _db.ServiceOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new { o.Id, o.Type, o.Title, o.Description })
            .FirstOrDefaultAsync();

        if (order == null) return NotFound();

        var activityType = NewWorkItem.Type == ServiceOrderType.Global
            ? ServiceOrderType.Preventivo
            : NewWorkItem.Type;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (order.Type != ServiceOrderType.Global)
            {
                var hasWorkItems = await _db.ServiceOrderWorkItems.AsNoTracking().AnyAsync(w => w.OrderId == id);
                if (!hasWorkItems)
                {
                    var first = new ServiceOrderWorkItem
                    {
                        OrderId = id,
                        SortOrder = 0,
                        Type = order.Type,
                        Title = string.IsNullOrWhiteSpace(order.Title) ? "Actividad 1" : order.Title,
                        Description = order.Description ?? "",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _db.ServiceOrderWorkItems.Add(first);
                    await _db.SaveChangesAsync();

                    await _db.ServiceOrderChecklistItems
                        .Where(x => x.OrderId == id && x.WorkItemId == null)
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.WorkItemId, first.Id));

                    await EnsureChecklistFromTemplateDbAsync(id, first.Type, first.Id);
                    await _db.SaveChangesAsync();
                }

                await _db.ServiceOrders
                    .Where(o => o.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Type, ServiceOrderType.Global));
            }

            var maxSort = await _db.ServiceOrderWorkItems
                .AsNoTracking()
                .Where(w => w.OrderId == id)
                .Select(w => (int?)w.SortOrder)
                .MaxAsync();

            var nextSort = (maxSort ?? -1) + 1;

            var wi = new ServiceOrderWorkItem
            {
                OrderId = id,
                SortOrder = nextSort,
                Type = activityType,
                Title = (NewWorkItem.Title ?? "").Trim(),
                Description = (NewWorkItem.Description ?? "").Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ServiceOrderWorkItems.Add(wi);
            await _db.SaveChangesAsync();

            await EnsureChecklistFromTemplateDbAsync(id, wi.Type, wi.Id);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return RedirectToPage(new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            Error = "La orden cambio mientras agregabas la actividad. Recarga e intentalo de nuevo.";
            return await ReloadAndShowAsync(id);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task EnsureChecklistFromTemplateDbAsync(Guid orderId, ServiceOrderType type, Guid? workItemId)
    {
        var exists = await _db.ServiceOrderChecklistItems
            .AsNoTracking()
            .AnyAsync(x => x.OrderId == orderId && x.WorkItemId == workItemId);
        if (exists) return;

        var template = await _db.ServiceOrderChecklistTemplates
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.IsActive && t.Type == type)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync();

        if (template == null || template.Items.Count == 0)
            return;

        var items = template.Items
            .OrderBy(x => x.SortOrder)
            .Select(it => new ServiceOrderChecklistItem
            {
                OrderId = orderId,
                WorkItemId = workItemId,
                SortOrder = it.SortOrder,
                Category = it.Category,
                Title = it.Title,
                IsRequired = it.IsRequired,
                IsDone = false,
                Notes = ""
            })
            .ToList();

        _db.ServiceOrderChecklistItems.AddRange(items);
    }

    public async Task<IActionResult> OnPostDeleteWorkItemAsync(Guid id, Guid workItemId)
    {
        // borramos checklist de esa actividad primero (por si tu DB aÃºn no tiene FK cascade)
        var its = await _db.ServiceOrderChecklistItems
            .Where(x => x.OrderId == id && x.WorkItemId == workItemId)
            .ToListAsync();
        if (its.Count > 0)
            _db.ServiceOrderChecklistItems.RemoveRange(its);

        var wi = await _db.ServiceOrderWorkItems
            .FirstOrDefaultAsync(x => x.Id == workItemId && x.OrderId == id);
        if (wi != null)
            _db.ServiceOrderWorkItems.Remove(wi);

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // Handlers JSON
    public async Task<JsonResult> OnGetProjectsAsync(Guid clientId) =>
        new(await ServiceOrderFormData.ProjectsAsync(_db, clientId));

    public async Task<JsonResult> OnGetContractsAsync(Guid clientId, Guid? projectId) =>
        new(await ServiceOrderFormData.ContractsAsync(_db, clientId, projectId));

    private async Task<IActionResult> ReloadAndShowAsync(Guid id)
    {
        var order = await _db.ServiceOrders
            .Include(o => o.WorkItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        Input = new InputModel
        {
            Id = order.Id,
            Title = order.Title,
            Type = order.Type,
            Status = order.Status,
            ClientId = order.ClientId,
            ProjectId = order.ProjectId,
            ClientServiceContractId = order.ClientServiceContractId,
            AssignedUserId = order.AssignedUserId,
            StartDate = ServiceOrderFormData.EditableDate(order.StartedAt ?? order.CreatedAt),
            ExpectedEndDate = ServiceOrderFormData.EditableDate(order.EstimatedEndDate ?? (order.StartedAt ?? order.CreatedAt).AddDays(2)),
            Description = order.Description ?? ""
        };

        WorkItems = order.WorkItems.OrderBy(w => w.SortOrder).ToList();
        NewWorkItem.Type = order.Type == ServiceOrderType.Global ? ServiceOrderType.Preventivo : order.Type;

        await LoadListsAsync();

        return Page();
    }

    private async Task EnsureChecklistFromTemplateAsync(ServiceOrder order, ServiceOrderType type, Guid? workItemId)
    {
        if (order.Checklist.Any(x => x.WorkItemId == workItemId))
            return;

        var template = await _db.ServiceOrderChecklistTemplates
            .Include(t => t.Items)
            .Where(t => t.IsActive && t.Type == type)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync();

        if (template == null || template.Items.Count == 0)
            return;

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
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        var employees = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "FullName");

        var allTypes = Enum.GetValues<ServiceOrderType>()
            .DistinctBy(t => (int)t)
            .OrderBy(t => (int)t)
            .Select(t => new { Id = t, Name = t.GetDisplayName() })
            .ToList();

        TypeItems = new SelectList(allTypes, "Id", "Name");

        var workTypes = allTypes.Where(x => (ServiceOrderType)x.Id != ServiceOrderType.Global).ToList();
        WorkTypeItems = new SelectList(workTypes, "Id", "Name");

        var statuses = new[]
            {
                ServiceOrderStatus.Created,
                ServiceOrderStatus.InProgress,
                ServiceOrderStatus.InReview,
                ServiceOrderStatus.Finalized,
                ServiceOrderStatus.Rejected
            }
            .Select(s => new { Id = s, Name = s.GetDisplayName() })
            .ToList();

        StatusItems = new SelectList(statuses, "Id", "Name");

        var clientId = Input?.ClientId ?? Guid.Empty;
        ProjectItems = new SelectList(await ServiceOrderFormData.ProjectsAsync(_db, clientId), "Id", "Text", Input?.ProjectId);
        ContractOptions = await ServiceOrderFormData.ContractsAsync(_db, clientId, Input?.ProjectId);
        ContractItems = new SelectList(ContractOptions, "Id", "Text", Input?.ClientServiceContractId);
    }
}



