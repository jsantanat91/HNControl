using System.ComponentModel.DataAnnotations;
using System;
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
    public SelectList StatusItems { get; set; } = default!;
    public SelectList ProjectItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;

    [BindProperty] public InputModel? Input { get; set; }

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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadListsAsync();

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
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

        var order = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (order == null) return NotFound();

        order.Title = (Input.Title ?? "").Trim();
        order.Type = Input.Type;
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

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        var employees = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "FullName");

        TypeItems = new SelectList(Enum.GetValues<ServiceOrderType>()
            .Select(t => new { Id = t, Name = t.ToString() }), "Id", "Name");

        StatusItems = new SelectList(Enum.GetValues<ServiceOrderStatus>()
            .Select(s => new { Id = s, Name = s.ToString() }), "Id", "Name");

        ProjectItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
        ContractItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
    }
}