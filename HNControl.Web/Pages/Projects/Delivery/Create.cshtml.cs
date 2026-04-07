using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    private const int MaxRows = 5;
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
        [Required, MaxLength(320)] public string DeliveryLocation { get; set; } = "";

        [Required, MaxLength(200)] public string ReceiverName { get; set; } = "";
        [Required, EmailAddress, MaxLength(256)] public string ReceiverEmail { get; set; } = "";
        [MaxLength(40)] public string ReceiverPhone { get; set; } = "";

        [MaxLength(200)] public string ProjectTemplateName { get; set; } = "";
        [MaxLength(200)] public string AssignedTechnicianName { get; set; } = "";

        public string[] ServiceNames { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] ServiceModes { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] ServiceTerms { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] EquipmentNames { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] EquipmentQty { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();

        [DataType(DataType.Date)] public DateTime DeliveryDate { get; set; } = DateTime.Today;
    }

    public async Task OnGetAsync()
    {
        await LoadCatalogsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCatalogsAsync();
        NormalizeRows(Input);

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

        var deliveryPayload = BuildDeliveryPayload(Input);

        var entity = new ProjectDeliveryFormat
        {
            ClientId = Input.ClientId,
            ProjectId = Input.ProjectId,
            Title = Input.Title.Trim(),
            ServiceSummary = "__DELIVERYJSON__" + JsonSerializer.Serialize(deliveryPayload),
            EquipmentSummary = BuildReadableEquipment(Input),
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

    private static void NormalizeRows(InputModel input)
    {
        input.ServiceNames = NormalizeArray(input.ServiceNames);
        input.ServiceModes = NormalizeArray(input.ServiceModes);
        input.ServiceTerms = NormalizeArray(input.ServiceTerms);
        input.EquipmentNames = NormalizeArray(input.EquipmentNames);
        input.EquipmentQty = NormalizeArray(input.EquipmentQty);
    }

    private static string[] NormalizeArray(string[]? source)
    {
        var result = Enumerable.Repeat("", MaxRows).ToArray();
        if (source == null) return result;

        for (var i = 0; i < MaxRows && i < source.Length; i++)
            result[i] = (source[i] ?? "").Trim();

        return result;
    }

    private static object BuildDeliveryPayload(InputModel input)
    {
        var services = new List<object>();
        var equipment = new List<object>();

        for (var i = 0; i < MaxRows; i++)
        {
            services.Add(new
            {
                Servicio = input.ServiceNames[i],
                Modalidad = input.ServiceModes[i],
                Plazo = input.ServiceTerms[i]
            });

            equipment.Add(new
            {
                Equipo = input.EquipmentNames[i],
                Cantidad = input.EquipmentQty[i]
            });
        }

        return new
        {
            NOMBREPROYECTO = input.ProjectTemplateName,
            NOMBRETECNICO = input.AssignedTechnicianName,
            Services = services,
            Equipment = equipment
        };
    }

    private static string BuildReadableEquipment(InputModel input)
    {
        var lines = new List<string>();
        for (var i = 0; i < MaxRows; i++)
        {
            if (!string.IsNullOrWhiteSpace(input.EquipmentNames[i]) || !string.IsNullOrWhiteSpace(input.EquipmentQty[i]))
                lines.Add($"{input.EquipmentNames[i]} ({input.EquipmentQty[i]})".Trim());
        }

        return lines.Count == 0 ? "-" : string.Join("\n", lines);
    }
}
