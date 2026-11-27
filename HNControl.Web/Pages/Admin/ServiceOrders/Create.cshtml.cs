using System.ComponentModel.DataAnnotations;
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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ServiceOrderCreationNotifications _notifications;
    public CreateModel(ApplicationDbContext db, ServiceOrderCreationNotifications notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public List<Guid> InactiveClientIds { get; set; } = new();
    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList TypeItems { get; set; } = default!;

    public SelectList ProjectItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;
    public List<ServiceOrderFormData.ContractOption> ContractOptions { get; set; } = new();

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        public ClientContactSelection Contacts { get; set; } = new();
        public string DeliveryKind { get; set; } = "service";
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required] public ServiceOrderType Type { get; set; } = ServiceOrderType.Preventivo;

        [Required] public Guid ClientId { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? ClientServiceContractId { get; set; }

        public string? AssignedUserId { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime ExpectedEndDate { get; set; } = DateTime.Today.AddDays(2);

        [MaxLength(2000)] public string Description { get; set; } = "";
    }

    public async Task OnGetAsync(Guid? clientId = null, Guid? projectId = null, Guid? contractId = null)
    {
        if (clientId.HasValue && await _db.Clients.AnyAsync(c => c.Id == clientId && !c.IsTemporaryLead))
            Input.ClientId = clientId.Value;
        if (projectId.HasValue && await _db.Projects.AnyAsync(p => p.Id == projectId && p.ClientId == Input.ClientId))
            Input.ProjectId = projectId;
        if (contractId.HasValue)
        {
            var contract = await _db.ClientServiceContracts.AsNoTracking().FirstOrDefaultAsync(c =>
                c.Id == contractId && c.ClientId == Input.ClientId &&
                (!Input.ProjectId.HasValue || c.ProjectId == null || c.ProjectId == Input.ProjectId));
            if (contract != null)
            {
                Input.ClientServiceContractId = contract.Id;
                Input.Contacts = ClientContactRouting.Selection(contract.NotificationContactsJson);
                Input.ProjectId ??= contract.ProjectId;
            }
        }
        await LoadListsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        var collection = Input.Type == ServiceOrderType.RecoleccionEquipos;
        if (collection)
        {
            Input.DeliveryKind = "service";
            if (string.IsNullOrWhiteSpace(Input.Title)) Input.Title = "Recolección de equipos";
            ModelState.Remove("Input.Title");
            if (Input.Title.Length > 200) ModelState.AddModelError("Input.Title", "El título admite hasta 200 caracteres.");
            if (string.IsNullOrWhiteSpace(Input.Description)) ModelState.AddModelError("Input.Description", "Escribe la dirección de recolección.");
        }
        if (!ModelState.IsValid) return Page();

        if (!HNControl.Shared.OrderDeliveryData.Kinds.Contains(Input.DeliveryKind))
        {
            Error = "Selecciona servicio, entrega de hardware o ambos.";
            return Page();
        }

        var validClient = await _db.Clients
            .AnyAsync(c => c.Id == Input.ClientId && (c.IsActive || collection) && !c.IsTemporaryLead);
        if (!validClient)
        {
            Error = "Solo se permiten clientes activos (no prospectos).";
            return Page();
        }

        Error = await ServiceOrderFormData.ValidateAsync(_db, Input.ClientId, Input.ProjectId,
            Input.ClientServiceContractId, Input.StartDate, Input.ExpectedEndDate);
        if (Error != null) return Page();

        if (!string.IsNullOrWhiteSpace(Input.AssignedUserId) && !await _db.EmployeeProfiles.AnyAsync(e => e.UserId == Input.AssignedUserId.Trim() && e.IsActive))
        { Error = "Selecciona un empleado activo para asignar la orden."; return Page(); }

        string? contactsJson;
        try { contactsJson = await ClientContactRouting.CaptureAsync(_db, Input.ClientId, Input.Contacts); }
        catch (ArgumentException ex) { Error = ex.Message; return Page(); }
        var order = new ServiceOrder
        {
            NotificationContactsJson = contactsJson,
            DeliveryJson = collection ? "" : new HNControl.Shared.OrderDeliveryData { Kind = Input.DeliveryKind }.Serialize(),
            Title = (Input.Title ?? "").Trim(),
            Type = Input.DeliveryKind == "hardware" ? ServiceOrderType.EntregaHardware : Input.Type,
            Status = ServiceOrderStatus.Created,

            ClientId = Input.ClientId,
            ProjectId = Input.ProjectId,
            ClientServiceContractId = Input.ClientServiceContractId,

            AssignedUserId = string.IsNullOrWhiteSpace(Input.AssignedUserId) ? null : Input.AssignedUserId.Trim(),

            StartedAt = TimeUtil.UtcDate(Input.StartDate),
            EstimatedEndDate = TimeUtil.UtcDate(Input.ExpectedEndDate),

            Description = (Input.Description ?? "").Trim(),

            PublicToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            CurrentArea = ServiceOrderWorkflowArea.Levantamiento
        };

        _db.ServiceOrders.Add(order);

        if (order.Type != ServiceOrderType.Global && !collection)
            await EnsureChecklistFromTemplateAsync(order, order.Type, workItemId: null);

        _notifications.QueueCreated(order);
        await _db.SaveChangesAsync();
        TempData[ServiceOrderCreationNotifications.NoticeKey] = (await _notifications.NotifyCreatedAsync(order.Id)).UserMessage;

        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

    public async Task<JsonResult> OnGetProjectsAsync(Guid clientId) =>
        new(await ServiceOrderFormData.ProjectsAsync(_db, clientId));

    public async Task<JsonResult> OnGetContractsAsync(Guid clientId, Guid? projectId) =>
        new(await ServiceOrderFormData.ContractsAsync(_db, clientId, projectId));

    public async Task<JsonResult> OnGetContactsAsync(Guid clientId, Guid? contractId)
    {
        var saved = contractId.HasValue ? await _db.ClientServiceContracts.AsNoTracking()
            .Where(c => c.Id == contractId && c.ClientId == clientId).Select(c => c.NotificationContactsJson).FirstOrDefaultAsync() : null;
        return new(await ClientContactRouting.OptionsAsync(_db, clientId, saved));
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients
            .Where(c => !c.IsTemporaryLead)
            .OrderBy(c => c.Name)
            .ToListAsync();
        ClientItems = new SelectList(clients.Select(c => new { c.Id, Name = c.Name + (c.IsActive ? "" : " (Inactivo)") }), "Id", "Name");
        InactiveClientIds = clients.Where(c => !c.IsActive).Select(c => c.Id).ToList();

        var employees = await _db.EmployeeProfiles.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "FullName");

        TypeItems = new SelectList(Enum.GetValues<ServiceOrderType>()
            .Select(t => new { Id = t, Name = t.GetDisplayName() }), "Id", "Name");

        var clientId = Input?.ClientId ?? Guid.Empty;
        ProjectItems = new SelectList(await ServiceOrderFormData.ProjectsAsync(_db, clientId), "Id", "Text", Input?.ProjectId);
        ContractOptions = await ServiceOrderFormData.ContractsAsync(_db, clientId, Input?.ProjectId);
        ContractItems = new SelectList(ContractOptions, "Id", "Text", Input?.ClientServiceContractId);
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
}
