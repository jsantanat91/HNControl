using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Delivery;

[Authorize(Policy = "EmployeeOnly")]
public class CreateModel : PageModel
{
    private const int MaxRows = ProjectDeliveryPayload.MaxRows;
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    public CreateModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public IFormFile[] EvidenceFiles { get; set; } = [];
    public SelectList ClientItems { get; set; } = default!;
    public List<ProjectOption> ProjectItems { get; set; } = [];
    public Guid? BackClientId { get; set; }

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
        [MaxLength(200)] public string SegmentoLan { get; set; } = "";
        [MaxLength(120)] public string IpPublica { get; set; } = "";

        public string[] ServiceNames { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] ServiceModes { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] ServiceTerms { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] EquipmentNames { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();
        public string[] EquipmentQty { get; set; } = Enumerable.Repeat("", MaxRows).ToArray();

        [DataType(DataType.Date)] public DateTime DeliveryDate { get; set; } = DateTime.Today;
    }

    public Task<IActionResult> OnGetAsync(Guid? clientId)
    {
        return Task.FromResult<IActionResult>(AppRoles.IsGlobalAdmin(User)
            ? RedirectToPage("/Admin/ServiceOrders/Create", new { clientId = clientId })
            : RedirectToPage("/ServiceOrders/Index"));
    }

    public Task<IActionResult> OnPostAsync()
    {
        return Task.FromResult<IActionResult>(AppRoles.IsGlobalAdmin(User)
            ? RedirectToPage("/Admin/ServiceOrders/Create", new { clientId = Input.ClientId })
            : RedirectToPage("/ServiceOrders/Index"));
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

    private async Task AddEvidenceFilesAsync(ProjectDeliveryFormat entity, DeliveryTemplateData payload)
    {
        var files = (EvidenceFiles ?? []).Where(x => x is { Length: > 0 }).Take(ProjectDeliveryPayload.MaxEvidenceFiles).ToList();
        foreach (var file in files)
        {
            var saved = await _storage.SaveFileAsync(
                file,
                $"projects/delivery/{entity.Id}/evidence",
                $"evidence_{payload.Evidences.Count + 1}_{Guid.NewGuid():N}",
                [".jpg", ".jpeg", ".png"],
                8 * 1024 * 1024);

            payload.Evidences.Add(new DeliveryEvidenceRow
            {
                StoragePath = saved.storagePath,
                OriginalFileName = saved.originalName,
                ContentType = saved.contentType
            });
        }
    }

    private static DeliveryTemplateData BuildDeliveryPayload(InputModel input)
    {
        var payload = new DeliveryTemplateData
        {
            NOMBREPROYECTO = input.ProjectTemplateName,
            NOMBRETECNICO = input.AssignedTechnicianName,
            SEGMENTOLAN = input.SegmentoLan,
            IPPUBLICA = input.IpPublica
        };

        for (var i = 0; i < MaxRows; i++)
        {
            payload.Services.Add(new DeliveryServiceRow
            {
                Servicio = input.ServiceNames[i],
                Modalidad = input.ServiceModes[i],
                Plazo = input.ServiceTerms[i]
            });

            payload.Equipment.Add(new DeliveryEquipmentRow
            {
                Equipo = input.EquipmentNames[i],
                Cantidad = input.EquipmentQty[i]
            });
        }

        return payload;
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


