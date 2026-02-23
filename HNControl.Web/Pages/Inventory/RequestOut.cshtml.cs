using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class RequestOutModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public RequestOutModel(ApplicationDbContext db) => _db = db;

    public List<SelectListItem> ItemOptions { get; set; } = new();
    public List<SelectListItem> ProjectOptions { get; set; } = new();
    public List<SelectListItem> EmployeeOptions { get; set; } = new();
    public List<SelectListItem> ClientOptions { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public Guid ItemId { get; set; }

        [Range(1, 999999)]
        public decimal Quantity { get; set; } = 1;

        public Guid? ProjectId { get; set; }

        [Required]
        public string ResponsibleUserId { get; set; } = "";

        public Guid? AssignedClientId { get; set; }

        [MaxLength(120)]
        public string? SerialNumber { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadListsAsync();
        Input.ResponsibleUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (!ModelState.IsValid) return Page();

        var item = await _db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == Input.ItemId && i.IsActive);
        if (item == null)
        {
            ModelState.AddModelError(string.Empty, "El item no existe o está inactivo.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.ResponsibleUserId))
        {
            ModelState.AddModelError(string.Empty, "Selecciona un responsable.");
            return Page();
        }

        // Si es consumible, ignoramos cliente/serie (opcionales)
        var assignedClientId = item.IsConsumable ? null : Input.AssignedClientId;
        var serial = item.IsConsumable ? "" : (Input.SerialNumber ?? "").Trim();

        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var requesterProfile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == requesterId);
        var responsibleProfile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == Input.ResponsibleUserId);

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ItemId = item.Id,
            Type = InventoryMovementType.Out,
            Status = InventoryMovementStatus.Pending,
            Quantity = Input.Quantity,
            ProjectId = Input.ProjectId,
            ResponsibleUserId = Input.ResponsibleUserId,
            ResponsibleName = responsibleProfile?.FullName ?? "",
            AssignedClientId = assignedClientId,
            SerialNumber = serial,
            Notes = (Input.Notes ?? "").Trim(),
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = requesterId,
            RequestedByName = requesterProfile?.FullName ?? (User.Identity?.Name ?? "")
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("./MyRequests");
    }

    private async Task LoadListsAsync()
    {
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.Sku, i.QuantityOnHand, i.Unit, i.IsConsumable })
            .ToListAsync();

        ItemOptions = items.Select(i => new SelectListItem
        {
            Value = i.Id.ToString(),
            Text = $"{i.Name}{(string.IsNullOrWhiteSpace(i.Sku) ? "" : " [" + i.Sku + "]")} • Existencia: {i.QuantityOnHand} {i.Unit} • {(i.IsConsumable ? "Consumible" : "Hardware") }"
        }).ToList();

        var projects = await _db.Projects.AsNoTracking().OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Title).Take(200).ToListAsync();
        ProjectOptions = new List<SelectListItem> { new SelectListItem { Value = "", Text = "(Sin proyecto)" } };
        ProjectOptions.AddRange(projects.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Title }));

        var emps = await _db.EmployeeProfiles.AsNoTracking().OrderBy(e => e.FullName).ToListAsync();
        EmployeeOptions = emps.Select(e => new SelectListItem { Value = e.UserId, Text = e.FullName }).ToList();

        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ClientOptions = new List<SelectListItem> { new SelectListItem { Value = "", Text = "(Consumible / No aplica)" } };
        ClientOptions.AddRange(clients.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }));
    }
}
