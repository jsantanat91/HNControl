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

        [Required] public string AssignedUserId { get; set; } = "";

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
        await LoadListsAsync();

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
            StartDate = (order.StartedAt ?? order.CreatedAt).ToLocalTime().Date,
            ExpectedEndDate = (order.EstimatedEndDate ?? (order.StartedAt ?? order.CreatedAt).AddDays(2)).ToLocalTime().Date,
            Description = order.Description ?? ""
        };

        WorkItems = order.WorkItems.OrderBy(w => w.SortOrder).ToList();
        NewWorkItem.Type = order.Type == ServiceOrderType.Global ? ServiceOrderType.Preventivo : order.Type;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        if (Input == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        // Validación de consistencia
        if (Input.ClientServiceContractId.HasValue)
        {
            var ok = await _db.ClientServiceContracts.AnyAsync(c =>
                c.Id == Input.ClientServiceContractId.Value &&
                c.ClientId == Input.ClientId &&
                (!Input.ProjectId.HasValue || c.ProjectId == null || c.ProjectId == Input.ProjectId));

            if (!ok)
            {
                Error = "El contrato seleccionado no pertenece al cliente/proyecto.";
                return Page();
            }
        }

        var order = await _db.ServiceOrders
            .Include(o => o.WorkItems)
            .FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (order == null) return NotFound();

        order.Title = (Input.Title ?? "").Trim();

        // Si ya es Global (tiene actividades), no permitimos “bajar” el tipo.
        order.Type = (order.WorkItems.Count > 0 || order.Type == ServiceOrderType.Global)
            ? ServiceOrderType.Global
            : Input.Type;

        order.Status = Input.Status;

        order.ClientId = Input.ClientId;
        order.ProjectId = Input.ProjectId;
        order.ClientServiceContractId = Input.ClientServiceContractId;

        order.AssignedUserId = Input.AssignedUserId;

        order.StartedAt = TimeUtil.UtcDate(Input.StartDate);
        order.EstimatedEndDate = TimeUtil.UtcDate(Input.ExpectedEndDate);
        order.Description = (Input.Description ?? "").Trim();

        await _db.SaveChangesAsync();
        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

    public async Task<IActionResult> OnPostConvertToGlobalAsync(Guid id)
    {
        var order = await _db.ServiceOrders
            .Include(o => o.WorkItems)
            .Include(o => o.Checklist)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        if (order.WorkItems.Count > 0 || order.Type == ServiceOrderType.Global)
            return RedirectToPage(new { id });

        await using var tx = await _db.Database.BeginTransactionAsync();

        // 1) Crear primera actividad con el contenido de la orden
        var wi = new ServiceOrderWorkItem
        {
            OrderId = order.Id,
            SortOrder = 0,
            Type = order.Type,
            Title = string.IsNullOrWhiteSpace(order.Title) ? "Actividad 1" : order.Title,
            Description = order.Description ?? "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // OJO: no dependemos de que la colección WorkItems esté "loaded" para que EF inserte.
        _db.ServiceOrderWorkItems.Add(wi);
        order.WorkItems.Add(wi);

        // IMPORTANTE: Insertamos primero la actividad para que exista en DB antes de actualizar el checklist,
        // porque PostgreSQL valida el FK inmediatamente.
        await _db.SaveChangesAsync();

        // Validación defensiva: asegura que la actividad existe en DB antes de referenciarla en checklist.
        var wiExists = await _db.ServiceOrderWorkItems.AsNoTracking().AnyAsync(x => x.Id == wi.Id);
        if (!wiExists)
            throw new InvalidOperationException($"No se insertó la actividad {wi.Id} en ServiceOrderWorkItems. Revisa el mapeo EF/tabla.");

        // 2) Mover checklist “general” a esta actividad (para no perder lo ya hecho)
        foreach (var it in order.Checklist.Where(x => x.WorkItemId == null))
            it.WorkItemId = wi.Id;

        // Si la orden no tenía checklist previo, crea uno para la primera actividad.
        await EnsureChecklistFromTemplateAsync(order, wi.Type, workItemId: wi.Id);

        // 3) Marcar orden como global
        order.Type = ServiceOrderType.Global;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddWorkItemAsync(Guid id)
    {
        // Validamos SOLO NewWorkItem (evita que fallen los Required del form principal).
        ModelState.Clear();
        TryValidateModel(NewWorkItem, nameof(NewWorkItem));
        if (!ModelState.IsValid)
            return await ReloadAndShowAsync(id);

        // Guard rail: “Global” no es un tipo de actividad.
        var activityType = NewWorkItem.Type == ServiceOrderType.Global
            ? ServiceOrderType.Preventivo
            : NewWorkItem.Type;

        // Traemos lo mínimo de la orden (sin grafo trackeado).
        var order = await _db.ServiceOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new { o.Id, o.Type, o.Title, o.Description })
            .FirstOrDefaultAsync();

        if (order == null) return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            // --- Asegurar que la orden quede en modo Global (si aún no lo está) ---
            if (order.Type != ServiceOrderType.Global)
            {
                var hasWorkItems = await _db.ServiceOrderWorkItems
                    .AsNoTracking()
                    .AnyAsync(w => w.OrderId == order.Id);

                // Si no había actividades, creamos la primera con el contenido original de la orden.
                if (!hasWorkItems)
                {
                    var first = new ServiceOrderWorkItem
                    {
                        OrderId = order.Id,
                        SortOrder = 0,
                        Type = order.Type,
                        Title = string.IsNullOrWhiteSpace(order.Title) ? "Actividad 1" : order.Title,
                        Description = order.Description ?? "",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _db.ServiceOrderWorkItems.Add(first);
                    await _db.SaveChangesAsync();

                    // Mover checklist “general” (WorkItemId NULL) a la primera actividad sin cargar listas en memoria.
                    await _db.ServiceOrderChecklistItems
                        .Where(x => x.OrderId == order.Id && x.WorkItemId == null)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.WorkItemId, first.Id));

                    // Si no existía checklist, crear desde template para esa primera actividad.
                    await EnsureChecklistFromTemplateDbAsync(order.Id, first.Type, first.Id);
                    await _db.SaveChangesAsync();
                }

                // Marcar la orden como Global (sin grafo trackeado; evita efectos colaterales y “concurrency” raros).
                await _db.ServiceOrders
                    .Where(o => o.Id == order.Id && o.Type != ServiceOrderType.Global)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Type, ServiceOrderType.Global));
            }

            // --- Agregar nueva actividad (sin tocar colecciones navegacionales) ---
            var maxSort = await _db.ServiceOrderWorkItems
                .AsNoTracking()
                .Where(w => w.OrderId == order.Id)
                .Select(w => (int?)w.SortOrder)
                .MaxAsync();

            var nextSort = (maxSort ?? -1) + 1;

            var wi = new ServiceOrderWorkItem
            {
                OrderId = order.Id,
                SortOrder = nextSort,
                Type = activityType,
                Title = (NewWorkItem.Title ?? "").Trim(),
                Description = (NewWorkItem.Description ?? "").Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ServiceOrderWorkItems.Add(wi);
            await _db.SaveChangesAsync();

            // Defensivo: si por alguna razón la inserción fue “saltada” por DB (trigger, regla, etc.), lo detectamos aquí.
            var wiExists = await _db.ServiceOrderWorkItems
                .AsNoTracking()
                .AnyAsync(x => x.Id == wi.Id && x.OrderId == order.Id);

            if (!wiExists)
                throw new InvalidOperationException($"No se insertó la actividad {wi.Id} en ServiceOrderWorkItems. Revisa triggers/reglas o el mapeo EF/tabla.");

            // Checklist por actividad (desde template)
            await EnsureChecklistFromTemplateDbAsync(order.Id, wi.Type, wi.Id);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return RedirectToPage(new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            // Si alguien editó/borró algo mientras estabas aquí, evita pantalla amarilla.
            await tx.RollbackAsync();
            Error = "La orden cambió mientras agregabas la actividad. Recarga la página e inténtalo de nuevo.";
            return await ReloadAndShowAsync(id);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IActionResult> OnPostDeleteWorkItemAsync(Guid id, Guid workItemId)
    {
        // borramos checklist de esa actividad primero (por si tu DB aún no tiene FK cascade)
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
    public async Task<JsonResult> OnGetProjectsAsync(Guid clientId)
    {
        var items = await _db.Projects
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new { id = p.Id, text = p.Title })
            .ToListAsync();

        return new JsonResult(items);
    }

    public async Task<JsonResult> OnGetContractsAsync(Guid clientId, Guid? projectId)
    {
        var q = _db.ClientServiceContracts.AsQueryable().Where(c => c.ClientId == clientId);

        if (projectId.HasValue)
            q = q.Where(c => c.ProjectId == null || c.ProjectId == projectId);

        var items = await q
            .OrderBy(c => c.ServiceType)
            .ThenBy(c => c.Label)
            .Select(c => new
            {
                id = c.Id,
                text = $"{c.ServiceType} · {c.Label}" +
                       (c.ContractEndDate.HasValue ? $" · vence {c.ContractEndDate.Value:yyyy-MM-dd}" : "")
            })
            .ToListAsync();

        return new JsonResult(items);
    }

    private async Task<IActionResult> ReloadAndShowAsync(Guid id)
    {
        await LoadListsAsync();

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
            StartDate = (order.StartedAt ?? order.CreatedAt).ToLocalTime().Date,
            ExpectedEndDate = (order.EstimatedEndDate ?? (order.StartedAt ?? order.CreatedAt).AddDays(2)).ToLocalTime().Date,
            Description = order.Description ?? ""
        };

        WorkItems = order.WorkItems.OrderBy(w => w.SortOrder).ToList();
        NewWorkItem.Type = order.Type == ServiceOrderType.Global ? ServiceOrderType.Preventivo : order.Type;

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

    private async Task EnsureChecklistFromTemplateDbAsync(Guid orderId, ServiceOrderType type, Guid? workItemId)
    {
        // Evita depender de colecciones navegacionales trackeadas (principal causa de “concurrency” rara en posts parciales).
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

        var statuses = Enum.GetValues<ServiceOrderStatus>()
            .DistinctBy(s => (int)s)
            .OrderBy(s => (int)s)
            .Select(s => new { Id = s, Name = s.GetDisplayName() })
            .ToList();

        StatusItems = new SelectList(statuses, "Id", "Name");

        ProjectItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
        ContractItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
    }
}
