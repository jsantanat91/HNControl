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
    public CreateModel(ApplicationDbContext db) => _db = db;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList TypeItems { get; set; } = default!;

    public SelectList ProjectItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
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

        public string Description { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        await LoadListsAsync();

        if (Input.ClientId == Guid.Empty)
        {
            var firstClient = await _db.Clients.OrderBy(c => c.Name).Select(c => c.Id).FirstOrDefaultAsync();
            if (firstClient != Guid.Empty) Input.ClientId = firstClient;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        if (!ModelState.IsValid) return Page();

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

        var order = new ServiceOrder
        {
            Title = (Input.Title ?? "").Trim(),
            Type = Input.Type,
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

        if (order.Type != ServiceOrderType.Global)
            await EnsureChecklistFromTemplateAsync(order, order.Type, workItemId: null);

        await _db.SaveChangesAsync();

        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

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
                text = $"{c.ServiceType} - {c.Label}" +
                       (c.ContractEndDate.HasValue ? $" - vence {c.ContractEndDate.Value:yyyy-MM-dd}" : "")
            })
            .ToListAsync();

        return new JsonResult(items);
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        var employees = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "FullName");

        TypeItems = new SelectList(Enum.GetValues<ServiceOrderType>()
            .Select(t => new { Id = t, Name = t.GetDisplayName() }), "Id", "Name");

        ProjectItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
        ContractItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
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
