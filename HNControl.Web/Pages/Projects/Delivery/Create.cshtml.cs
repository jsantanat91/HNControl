using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Delivery;

[Authorize(Policy = "EmployeeOnly")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();
    public SelectList ClientItems { get; set; } = default!;
    public List<ProjectOption> ProjectItems { get; set; } = [];

    public record ProjectOption(Guid Id, Guid ClientId, string Name);

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }
        public Guid? ProjectId { get; set; }

        [Required, MaxLength(220)] public string Title { get; set; } = "";
        [Required, MaxLength(4000)] public string ServiceSummary { get; set; } = "";
        [MaxLength(4000)] public string EquipmentSummary { get; set; } = "";
        [Required, MaxLength(320)] public string DeliveryLocation { get; set; } = "";

        [Required, MaxLength(200)] public string ReceiverName { get; set; } = "";
        [Required, EmailAddress, MaxLength(256)] public string ReceiverEmail { get; set; } = "";
        [MaxLength(40)] public string ReceiverPhone { get; set; } = "";

        [DataType(DataType.Date)] public DateTime DeliveryDate { get; set; } = DateTime.Today;
    }

    public async Task OnGetAsync()
    {
        await LoadCatalogsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCatalogsAsync();
        if (!ModelState.IsValid) return Page();

        if (Input.ProjectId.HasValue)
        {
            var projectBelongsToClient = await _db.Projects
                .AsNoTracking()
                .AnyAsync(x => x.Id == Input.ProjectId.Value && x.ClientId == Input.ClientId);
            if (!projectBelongsToClient)
            {
                ModelState.AddModelError("Input.ProjectId", "El proyecto no pertenece al cliente seleccionado.");
                return Page();
            }
        }

        var entity = new ProjectDeliveryFormat
        {
            ClientId = Input.ClientId,
            ProjectId = Input.ProjectId,
            Title = Input.Title.Trim(),
            ServiceSummary = Input.ServiceSummary.Trim(),
            EquipmentSummary = (Input.EquipmentSummary ?? "").Trim(),
            DeliveryLocation = Input.DeliveryLocation.Trim(),
            ReceiverName = Input.ReceiverName.Trim(),
            ReceiverEmail = Input.ReceiverEmail.Trim(),
            ReceiverPhone = (Input.ReceiverPhone ?? "").Trim(),
            DeliveryDate = Input.DeliveryDate.Date,
            PublicToken = Guid.NewGuid().ToString("N"),
            TokenExpiresAt = DateTime.UtcNow.AddMonths(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ProjectDeliveryFormats.Add(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("/Projects/Delivery/Details", new { id = entity.Id });
    }

    private async Task LoadCatalogsAsync()
    {
        var clients = await _db.Clients
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        var projects = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectOption(x.Id, x.ClientId, x.Title))
            .ToListAsync();
        ProjectItems = projects;
    }
}

