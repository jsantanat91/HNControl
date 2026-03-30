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

    public class LineInput
    {
        [Required]
        public Guid ItemId { get; set; }

        [Range(1, 999999)]
        public decimal Quantity { get; set; } = 1;

        public Guid? AssignedClientId { get; set; }

        [MaxLength(120)]
        public string? SerialNumber { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class InputModel
    {
        public Guid? ProjectId { get; set; }

        [Required]
        public string ResponsibleUserId { get; set; } = "";

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public List<LineInput> Lines { get; set; } = new() { new LineInput() };
    }

    public async Task OnGetAsync()
    {
        await LoadListsAsync();
        Input.ResponsibleUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (Input.Lines == null || Input.Lines.Count == 0)
            Input.Lines = new List<LineInput> { new LineInput() };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        var lines = (Input.Lines ?? new List<LineInput>())
            .Where(l => l.ItemId != Guid.Empty && l.Quantity > 0)
            .ToList();

        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Agrega al menos un item.");

        if (!ModelState.IsValid)
            return Page();

        if (string.IsNullOrWhiteSpace(Input.ResponsibleUserId))
        {
            ModelState.AddModelError(string.Empty, "Selecciona un responsable.");
            return Page();
        }

        var ids = lines.Select(x => x.ItemId).Distinct().ToList();
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.IsActive)
            .Select(i => new { i.Id, i.IsConsumable })
            .ToListAsync();

        var itemMap = items.ToDictionary(x => x.Id, x => x);
        foreach (var l in lines)
        {
            if (!itemMap.ContainsKey(l.ItemId))
            {
                ModelState.AddModelError(string.Empty, "Uno o mas items no existen o estan inactivos.");
                return Page();
            }
        }

        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var requesterProfile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == requesterId);
        var responsibleProfile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == Input.ResponsibleUserId);

        var now = DateTime.UtcNow;
        var globalNotes = (Input.Notes ?? "").Trim();

        foreach (var l in lines)
        {
            var item = itemMap[l.ItemId];

            var assignedClientId = item.IsConsumable ? null : l.AssignedClientId;
            var serial = item.IsConsumable ? "" : (l.SerialNumber ?? "").Trim();

            var lineNotes = (l.Notes ?? "").Trim();
            var notes = string.Join("\n", new[] { globalNotes, lineNotes }.Where(x => !string.IsNullOrWhiteSpace(x)));

            _db.InventoryMovements.Add(new InventoryMovement
            {
                ItemId = l.ItemId,
                Type = InventoryMovementType.Out,
                Status = InventoryMovementStatus.Pending,
                Quantity = l.Quantity,
                ProjectId = Input.ProjectId,
                ResponsibleUserId = Input.ResponsibleUserId,
                ResponsibleName = responsibleProfile?.FullName ?? "",
                AssignedClientId = assignedClientId,
                SerialNumber = serial,
                Notes = notes,
                RequestedAt = now,
                RequestedByUserId = requesterId,
                RequestedByName = requesterProfile?.FullName ?? (User.Identity?.Name ?? "")
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("./MyRequests");
    }

    private async Task LoadListsAsync()
    {
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.ModelCode, i.Sku, i.Category, i.Location, i.QuantityOnHand, i.Unit, i.IsConsumable })
            .ToListAsync();

        ItemOptions = items.Select(i => new SelectListItem
        {
            Value = i.Id.ToString(),
            Text = $"{i.Name} · ID: {(string.IsNullOrWhiteSpace(i.ModelCode) ? "-" : i.ModelCode)}{(string.IsNullOrWhiteSpace(i.Sku) ? "" : " · SKU: " + i.Sku)}" +
                   $" · {(string.IsNullOrWhiteSpace(i.Category) ? "Sin categoria" : i.Category)}" +
                   $" · {(string.IsNullOrWhiteSpace(i.Location) ? "-" : i.Location)}" +
                   $" · Existencia: {i.QuantityOnHand} {i.Unit}"
        }).ToList();

        var projects = await _db.Projects.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Title)
            .Take(200)
            .ToListAsync();

        ProjectOptions = new List<SelectListItem> { new SelectListItem { Value = "", Text = "(Sin proyecto)" } };
        ProjectOptions.AddRange(projects.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Title }));

        var emps = await _db.EmployeeProfiles.AsNoTracking().OrderBy(e => e.FullName).ToListAsync();
        EmployeeOptions = emps.Select(e => new SelectListItem { Value = e.UserId, Text = e.FullName }).ToList();

        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ClientOptions = new List<SelectListItem> { new SelectListItem { Value = "", Text = "(Consumible / No aplica)" } };
        ClientOptions.AddRange(clients.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }));
    }
}
