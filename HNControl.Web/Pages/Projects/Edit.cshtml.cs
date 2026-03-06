using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    public record ContractOption(Guid Id, string Name);

    private readonly ApplicationDbContext _db;

    public EditModel(ApplicationDbContext db) => _db = db;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }

        [Required(ErrorMessage = "Cliente es requerido.")]
        public Guid ClientId { get; set; }

        [Required(ErrorMessage = "Responsable es requerido.")]
        public string ResponsibleUserId { get; set; } = "";

        [Required(ErrorMessage = "El nombre del proyecto es requerido.")]
        [MaxLength(200)]
        public string Title { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        [DataType(DataType.Date)]
        public DateTime? EstimatedEndDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);

        [Required] public ProjectStatus Status { get; set; } = ProjectStatus.Open;

        [MaxLength(400)]
        public string Objective { get; set; } = "";

        [MaxLength(1200)]
        public string Scope { get; set; } = "";

        public string Description { get; set; } = "";
        public string AccessNotes { get; set; } = "";
        public string Comments { get; set; } = "";
        public List<Guid> ContractIds { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        var linkedContractIds = await _db.ClientServiceContracts
            .Where(c => c.ProjectId == p.Id)
            .Select(c => c.Id)
            .ToListAsync();

        Input = new InputModel
        {
            Id = p.Id,
            ClientId = p.ClientId,
            ResponsibleUserId = p.AssignedUserId,
            Title = p.Title,
            StartDate = p.StartDate.Date,
            EstimatedEndDate = p.EstimatedEndDate.Date,
            Status = p.Status,
            Objective = p.Objective,
            Scope = p.Scope,
            Description = p.ActivityDescription,
            AccessNotes = p.AccessNotes,
            Comments = p.AdditionalComments,
            ContractIds = linkedContractIds
        };

        await LoadListsAsync();
        await LoadContractItemsAsync(Input.ClientId, Input.ContractIds);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        await LoadContractItemsAsync(Input.ClientId, Input.ContractIds);

        if (!ModelState.IsValid)
            return Page();

        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (p == null) return NotFound();

        var end = Input.EstimatedEndDate ?? Input.StartDate.AddDays(7);

        var startUtc = TimeUtil.UtcDate(Input.StartDate);
        var endUtc = TimeUtil.UtcDate(end);
        if (endUtc < startUtc)
        {
            Error = "La fecha estimada no puede ser menor al inicio.";
            return Page();
        }

        p.ClientId = Input.ClientId;
        p.AssignedUserId = Input.ResponsibleUserId;
        p.Title = (Input.Title ?? "").Trim();
        p.StartDate = startUtc;
        p.EstimatedEndDate = endUtc;
        p.Status = Input.Status;
        p.Objective = (Input.Objective ?? "").Trim();
        p.Scope = (Input.Scope ?? "").Trim();
        p.ActivityDescription = (Input.Description ?? "").Trim();
        p.AccessNotes = (Input.AccessNotes ?? "").Trim();
        p.AdditionalComments = (Input.Comments ?? "").Trim();
        p.UpdatedAt = DateTime.UtcNow;

        if (p.Status == ProjectStatus.Closed && p.ClosedAt == null)
        {
            p.ClosedAt = DateTime.UtcNow;
            p.ClosedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        else if (p.Status == ProjectStatus.Open)
        {
            p.ClosedAt = null;
            p.ClosedByUserId = null;
        }

        await _db.SaveChangesAsync();
        await AssignContractsAsync(p.Id, Input.ClientId, Input.ContractIds);

        return RedirectToPage("/Projects/Details", new { id = p.Id });
    }

    public async Task<IActionResult> OnGetContractsAsync(Guid clientId)
    {
        var rows = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderBy(c => c.ServiceType)
            .ThenBy(c => c.Label)
            .Select(c => new
            {
                c.Id,
                text = $"{c.Label} [{c.ServiceType}] · Cuenta {c.AccountNumber}"
            })
            .ToListAsync();

        return new JsonResult(rows);
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name", Input.ClientId);

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(emps, "UserId", "FullName", Input.ResponsibleUserId);
    }

    private async Task LoadContractItemsAsync(Guid clientId, List<Guid>? selectedIds = null)
    {
        var rows = clientId == Guid.Empty
            ? new List<ContractOption>()
            : await _db.ClientServiceContracts
                .AsNoTracking()
                .Where(c => c.ClientId == clientId)
                .OrderBy(c => c.ServiceType)
                .ThenBy(c => c.Label)
                .Select(c => new ContractOption(
                    c.Id,
                    $"{c.Label} [{c.ServiceType}] · Cuenta {c.AccountNumber}"))
                .ToListAsync();

        ContractItems = new SelectList(rows, "Id", "Name", selectedIds ?? new List<Guid>());
    }

    private async Task AssignContractsAsync(Guid projectId, Guid clientId, List<Guid>? selectedIds)
    {
        selectedIds ??= new List<Guid>();
        var selected = selectedIds.Distinct().ToHashSet();

        var contracts = await _db.ClientServiceContracts
            .Where(c => c.ClientId == clientId)
            .ToListAsync();

        foreach (var c in contracts)
        {
            if (selected.Contains(c.Id))
                c.ProjectId = projectId;
            else if (c.ProjectId == projectId)
                c.ProjectId = null;
        }

        await _db.SaveChangesAsync();
    }
}
