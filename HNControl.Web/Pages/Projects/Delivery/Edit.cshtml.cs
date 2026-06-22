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
public class EditModel : PageModel
{
    private const int MaxRows = ProjectDeliveryPayload.MaxRows;
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public EditModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public IFormFile[] EvidenceFiles { get; set; } = [];
    public SelectList ClientItems { get; set; } = default!;
    public List<ProjectOption> ProjectItems { get; set; } = [];
    public List<DeliveryEvidenceRow> ExistingEvidences { get; set; } = [];
    public Guid DeliveryId { get; set; }

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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var item = await _db.ProjectDeliveryFormats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        DeliveryId = id;
        await LoadCatalogsAsync();
        FillInput(item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        DeliveryId = id;
        await LoadCatalogsAsync();
        NormalizeRows(Input);

        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        ExistingEvidences = ProjectDeliveryPayload.Parse(item.ServiceSummary).Evidences;

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

        var previousPayload = ProjectDeliveryPayload.Parse(item.ServiceSummary);
        var deliveryPayload = BuildDeliveryPayload(Input);
        deliveryPayload.Evidences = previousPayload.Evidences
            .Where(x => !string.IsNullOrWhiteSpace(x.StoragePath))
            .Take(ProjectDeliveryPayload.MaxEvidenceFiles)
            .ToList();
        await AddEvidenceFilesAsync(item, deliveryPayload);

        item.ClientId = Input.ClientId;
        item.ProjectId = Input.ProjectId;
        item.Title = Input.Title.Trim();
        item.ServiceSummary = ProjectDeliveryPayload.Serialize(deliveryPayload);
        item.EquipmentSummary = BuildReadableEquipment(Input);
        item.DeliveryLocation = Input.DeliveryLocation.Trim();
        item.ReceiverName = Input.ReceiverName.Trim();
        item.ReceiverEmail = Input.ReceiverEmail.Trim();
        item.ReceiverPhone = (Input.ReceiverPhone ?? "").Trim();
        item.DeliveryDate = Input.DeliveryDate.Date;
        item.PdfStoragePath = null;
        item.PdfGeneratedAt = null;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Projects/Delivery/Details", new { id = item.Id, clientId = item.ClientId });
    }

    public async Task<IActionResult> OnPostRemoveEvidenceAsync(Guid id, string storagePath)
    {
        var item = await _db.ProjectDeliveryFormats.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        var payload = ProjectDeliveryPayload.Parse(item.ServiceSummary);
        var evidence = payload.Evidences.FirstOrDefault(x => string.Equals(x.StoragePath, storagePath, StringComparison.Ordinal));
        if (evidence != null)
        {
            payload.Evidences.Remove(evidence);
            await _storage.DeleteIfExistsAsync(evidence.StoragePath ?? "");
            item.ServiceSummary = ProjectDeliveryPayload.Serialize(payload);
            item.PdfStoragePath = null;
            item.PdfGeneratedAt = null;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    private void FillInput(ProjectDeliveryFormat item)
    {
        var tpl = ProjectDeliveryPayload.Parse(item.ServiceSummary);
        ExistingEvidences = tpl.Evidences;
        Input = new InputModel
        {
            ClientId = item.ClientId,
            ProjectId = item.ProjectId,
            Title = item.Title,
            DeliveryLocation = item.DeliveryLocation,
            ReceiverName = item.ReceiverName,
            ReceiverEmail = item.ReceiverEmail,
            ReceiverPhone = item.ReceiverPhone,
            ProjectTemplateName = tpl.NOMBREPROYECTO ?? "",
            AssignedTechnicianName = tpl.NOMBRETECNICO ?? "",
            SegmentoLan = tpl.SEGMENTOLAN ?? "",
            IpPublica = tpl.IPPUBLICA ?? "",
            DeliveryDate = item.DeliveryDate.Date
        };

        for (var i = 0; i < MaxRows; i++)
        {
            var s = i < tpl.Services.Count ? tpl.Services[i] : new DeliveryServiceRow();
            var e = i < tpl.Equipment.Count ? tpl.Equipment[i] : new DeliveryEquipmentRow();
            Input.ServiceNames[i] = s.Servicio ?? "";
            Input.ServiceModes[i] = s.Modalidad ?? "";
            Input.ServiceTerms[i] = s.Plazo ?? "";
            Input.EquipmentNames[i] = e.Equipo ?? "";
            Input.EquipmentQty[i] = e.Cantidad ?? "";
        }
    }

    private async Task LoadCatalogsAsync()
    {
        var clients = await _db.Clients
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        ProjectItems = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectOption(x.Id, x.ClientId, x.Title))
            .ToListAsync();
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
        var remaining = Math.Max(0, ProjectDeliveryPayload.MaxEvidenceFiles - payload.Evidences.Count);
        var files = (EvidenceFiles ?? []).Where(x => x is { Length: > 0 }).Take(remaining).ToList();
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
