using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients.Services;

[Authorize]
public class CreateModel : PageModel
{
    private const string ServicePackageMarker = "INV_SERVICE_PACKAGE";

    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ISecretProtector _protector;

    public CreateModel(ApplicationDbContext db, IFileStorage storage, ISecretProtector protector)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
    }

    [BindProperty(SupportsGet = true)]
    public Guid ClientId { get; set; }
    [BindProperty(SupportsGet = true)]
    public Guid? FeasibilityId { get; set; }

    public string ClientName { get; set; } = "";
    public string ClientCode { get; set; } = "";

    public SelectList ServiceTypeItems =>
        new(Enum.GetValues<ClientServiceType>().Select(x => new { Id = x, Name = x.ToString() }), "Id", "Name");

    public SelectList ProjectItems { get; set; } = default!;
    public SelectList ServicePackageItems { get; set; } = default!;
    public SelectList SalesOpportunityItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        public Guid? ServicePackageId { get; set; }

        [Required] public ClientServiceType ServiceType { get; set; } = ClientServiceType.Internet;

        public List<string> SelectedServiceTypes { get; set; } = ["Internet"];

        [Required, MaxLength(200)]
        public string Label { get; set; } = "";

        [MaxLength(120)]
        public string Provider { get; set; } = "";

        [MaxLength(120)]
        public string AccountNumber { get; set; } = "";

        [MaxLength(120)]
        public string ContractNumber { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime? ContractStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ContractEndDate { get; set; }

        public Guid? ProjectId { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [Range(0, 99999999)]
        public decimal? MonthlyAmount { get; set; }

        [Range(0, 99999999)]
        public decimal InstallationCost { get; set; } = 0m;

        [MaxLength(20)] public string InternetCapacity { get; set; } = "";
        [MaxLength(40)] public string InternetCapacityOther { get; set; } = "";
        [MaxLength(20)] public string TelephonyExtensions { get; set; } = "";
        [MaxLength(20)] public string TelephonyTrunks { get; set; } = "";
        [MaxLength(20)] public string TelephonyDids { get; set; } = "";
        [MaxLength(20)] public string CctvChannels { get; set; } = "";
        [MaxLength(40)] public string CctvChannelsOther { get; set; } = "";
        [MaxLength(40)] public string SecurityBrand { get; set; } = "";
        [MaxLength(80)] public string SecurityBrandOther { get; set; } = "";
        [MaxLength(80)] public string ServerOs { get; set; } = "";
        [MaxLength(20)] public string ServerCpuCores { get; set; } = "";
        [MaxLength(40)] public string ServerRam { get; set; } = "";
        [MaxLength(80)] public string ServerDisk { get; set; } = "";

        [MaxLength(20)]
        public string BillingRecurrence { get; set; } = "Mensual";

        [MaxLength(20)]
        public string ContractTermOption { get; set; } = "12";

        public Guid? SalesOpportunityId { get; set; }

        [MaxLength(140)]
        public string Branch { get; set; } = "";

        [MaxLength(320)]
        public string BranchAddress { get; set; } = "";

        public IFormFile? SignedContract { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == ClientId);
        if (client == null) return NotFound();

        if (string.IsNullOrWhiteSpace(client.ClientCode))
        {
            client.ClientCode = await NextClientCodeAsync();
            await _db.SaveChangesAsync();
        }

        ClientName = client.Name;
        ClientCode = client.ClientCode;
        await LoadProjectsAsync();
        await LoadServicePackagesAsync();
        await LoadSalesOpportunitiesAsync();
        await PrefillFromFeasibilityAsync();

        if (Input.ContractStartDate == null)
            Input.ContractStartDate = DateTime.Today;
        if (string.IsNullOrWhiteSpace(Input.AccountNumber))
            Input.AccountNumber = client.ClientCode;
        if (string.IsNullOrWhiteSpace(Input.ContractNumber))
        {
            var count = await _db.ClientServiceContracts.CountAsync(c => c.ClientId == ClientId);
            Input.ContractNumber = $"{client.ClientCode}-{count + 1:00}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == ClientId);
        if (client == null) return NotFound();

        if (string.IsNullOrWhiteSpace(client.ClientCode))
        {
            client.ClientCode = await NextClientCodeAsync();
            await _db.SaveChangesAsync();
        }

        ClientName = client.Name;
        ClientCode = client.ClientCode;
        await LoadProjectsAsync();
        await LoadServicePackagesAsync();
        await LoadSalesOpportunitiesAsync();

        if (Input.ServicePackageId.HasValue && string.IsNullOrWhiteSpace(Input.Label))
        {
            var packageName = await _db.QuoteCatalogItems
                .AsNoTracking()
                .Where(x => x.Id == Input.ServicePackageId.Value && x.VariantGroup == ServicePackageMarker)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                Input.Label = packageName;
                ModelState.Remove("Input.Label");
            }
        }

        NormalizeSelectedServiceTypes();
        ModelState.Remove("Input.ServiceType");
        ModelState.Remove("Input.SelectedServiceTypes");

        if (!ModelState.IsValid) return Page();

        QuoteCatalogItem? selectedPackage = null;
        if (Input.ServicePackageId.HasValue)
        {
            selectedPackage = await _db.QuoteCatalogItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == Input.ServicePackageId.Value && x.VariantGroup == ServicePackageMarker);
        }

        var serviceType = PrimaryServiceType(Input.SelectedServiceTypes, Input.ServiceType);
        if (selectedPackage != null && Enum.TryParse<ClientServiceType>(selectedPackage.VariantValue, true, out var parsedType))
            serviceType = parsedType;

        var contract = new ClientServiceContract
        {
            ClientId = ClientId,
            ProjectId = Input.ProjectId,
            ServiceType = serviceType,
            Label = string.IsNullOrWhiteSpace(Input.Label) && selectedPackage != null ? selectedPackage.Name : (Input.Label ?? "").Trim(),
            Provider = (Input.Provider ?? "").Trim(),
            AccountNumber = string.IsNullOrWhiteSpace(Input.AccountNumber) ? client.ClientCode : Input.AccountNumber.Trim(),
            ContractNumber = string.IsNullOrWhiteSpace(Input.ContractNumber)
                ? $"{client.ClientCode}-{await _db.ClientServiceContracts.CountAsync(c => c.ClientId == ClientId) + 1:00}"
                : Input.ContractNumber.Trim(),
            ContractStartDate = Input.ContractStartDate?.Date,
            ContractEndDate = Input.ContractEndDate?.Date,
            Notes = ComposeNotesMetadata(
                ComposeFeasibilityMetadata((Input.Notes ?? "").Trim(), FeasibilityId),
                Input.BillingRecurrence,
                Input.ContractTermOption,
                Input.SalesOpportunityId,
                Input.InstallationCost,
                Input.SelectedServiceTypes,
                Input.InternetCapacity,
                Input.InternetCapacityOther,
                Input.TelephonyExtensions,
                Input.TelephonyTrunks,
                Input.TelephonyDids,
                Input.CctvChannels,
                Input.CctvChannelsOther,
                Input.SecurityBrand,
                Input.SecurityBrandOther,
                Input.ServerOs,
                Input.ServerCpuCores,
                Input.ServerRam,
                Input.ServerDisk),
            MonthlyAmount = Input.MonthlyAmount ?? selectedPackage?.UnitPrice,
            Branch = (Input.Branch ?? "").Trim(),
            BranchAddress = (Input.BranchAddress ?? "").Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClientServiceContracts.Add(contract);
        await _db.SaveChangesAsync();

        if (Input.SignedContract != null && Input.SignedContract.Length > 0)
        {
            try
            {
                var (path, size, contentType, original) = await _storage.SaveFileAsync(
                    Input.SignedContract,
                    subFolder: $"client-contracts/{ClientId}",
                    fileNameNoExt: $"contract_{contract.Id}",
                    allowedExtensions: new[] { ".pdf" },
                    maxBytes: 20L * 1024L * 1024L);

                contract.SignedContractStoragePath = path;
                contract.SignedContractSizeBytes = size;
                contract.SignedContractContentType = contentType;
                contract.SignedContractOriginalFileName = original;
                contract.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                return Page();
            }
        }

        if (FeasibilityId.HasValue)
        {
            var feasibility = await _db.ServiceFeasibilities
                .FirstOrDefaultAsync(x => x.Id == FeasibilityId.Value && x.ClientId == ClientId);
            if (feasibility != null)
            {
                var note = feasibility.Notes ?? "";
                var marker = $"[META] ContratoId={contract.Id}";
                if (!note.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(note))
                        note += Environment.NewLine;
                    note += marker;
                    feasibility.Notes = note.Length <= 2000 ? note : note[..2000];
                    feasibility.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
        }

        return RedirectToPage("/Clients/Details", new { id = ClientId });
    }

    private async Task<string> NextClientCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-"))
            .Select(c => c.ClientCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            if (int.TryParse(code.AsSpan(3), out var n) && n > max)
                max = n;
        }

        return $"HN-{max + 1:0000}";
    }

    private async Task LoadProjectsAsync()
    {
        var projs = await _db.Projects
            .Where(p => p.ClientId == ClientId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new { p.Id, p.Title })
            .ToListAsync();

        ProjectItems = new SelectList(projs, "Id", "Title");
    }

    private async Task LoadServicePackagesAsync()
    {
        var rows = await _db.QuoteCatalogItems
            .AsNoTracking()
            .Where(x => x.VariantGroup == ServicePackageMarker && x.NodeType == QuoteNodeType.Service && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                Label = x.Name + (x.UnitPrice.HasValue ? $" · {x.UnitPrice.Value.ToString("C2")}" : " · Precio manual")
            })
            .ToListAsync();

        ServicePackageItems = new SelectList(rows, "Id", "Label");
    }

    private async Task LoadSalesOpportunitiesAsync()
    {
        var rows = await _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Where(x => x.ClientId == ClientId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.Id,
                Label = (x.QuoteRequest != null ? x.QuoteRequest.Folio : "Venta")
                    + " · " + x.WorkflowStage
                    + " · " + x.Status
            })
            .ToListAsync();

        SalesOpportunityItems = new SelectList(rows, "Id", "Label");
    }

    private static string ComposeNotesMetadata(
        string rawNotes,
        string recurrence,
        string termOption,
        Guid? salesOpportunityId,
        decimal installationCost,
        IReadOnlyCollection<string> selectedServiceTypes,
        string internetCapacity,
        string internetCapacityOther,
        string telephonyExtensions,
        string telephonyTrunks,
        string telephonyDids,
        string cctvChannels,
        string cctvChannelsOther,
        string securityBrand,
        string securityBrandOther,
        string serverOs,
        string serverCpuCores,
        string serverRam,
        string serverDisk)
    {
        var notes = StripNotesMetadata(rawNotes);
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(notes))
            lines.Add(notes);
        lines.Add($"[META] Recurrencia={NormalizeRecurrence(recurrence)}");
        lines.Add($"[META] Plazo={NormalizeTerm(termOption)}");
        lines.Add($"[META] CostoInstalacion={Math.Max(0m, installationCost).ToString("0.##", CultureInfo.InvariantCulture)}");
        lines.Add($"[META] TiposServicio={string.Join('|', NormalizeServiceTypeNames(selectedServiceTypes))}");
        AddMeta(lines, "InternetCapacidad", internetCapacity);
        AddMeta(lines, "InternetCapacidadOtro", internetCapacityOther);
        AddMeta(lines, "TelefoniaExtensiones", telephonyExtensions);
        AddMeta(lines, "TelefoniaTroncales", telephonyTrunks);
        AddMeta(lines, "TelefoniaDID", telephonyDids);
        AddMeta(lines, "CCTVCanales", cctvChannels);
        AddMeta(lines, "CCTVCanalesOtro", cctvChannelsOther);
        AddMeta(lines, "SeguridadMarca", securityBrand);
        AddMeta(lines, "SeguridadMarcaOtro", securityBrandOther);
        AddMeta(lines, "ServidorSO", serverOs);
        AddMeta(lines, "ServidorNucleos", serverCpuCores);
        AddMeta(lines, "ServidorRAM", serverRam);
        AddMeta(lines, "ServidorDisco", serverDisk);
        if (salesOpportunityId.HasValue)
            lines.Add($"[META] VentaId={salesOpportunityId.Value}");
        return string.Join(Environment.NewLine, lines);
    }

    private void NormalizeSelectedServiceTypes()
    {
        Input.SelectedServiceTypes = NormalizeServiceTypeNames(Input.SelectedServiceTypes).ToList();
        if (!Input.SelectedServiceTypes.Any())
            Input.SelectedServiceTypes = [Enum.IsDefined(Input.ServiceType) ? Input.ServiceType.ToString() : ClientServiceType.Internet.ToString()];
    }

    private static IEnumerable<string> NormalizeServiceTypeNames(IEnumerable<string>? selected)
    {
        var allowed = Enum.GetNames<ClientServiceType>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (selected ?? [])
            .Select(x => (x ?? "").Trim())
            .Where(x => allowed.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static ClientServiceType PrimaryServiceType(IEnumerable<string> selected, ClientServiceType fallback)
    {
        var first = NormalizeServiceTypeNames(selected).FirstOrDefault();
        return Enum.TryParse<ClientServiceType>(first, true, out var parsed) ? parsed : fallback;
    }

    private static void AddMeta(List<string> lines, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"[META] {key}={value.Trim()}");
    }

    private async Task PrefillFromFeasibilityAsync()
    {
        if (!FeasibilityId.HasValue)
            return;

        var feasibility = await _db.ServiceFeasibilities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == FeasibilityId.Value && x.ClientId == ClientId);
        if (feasibility == null)
            return;

        if (string.IsNullOrWhiteSpace(Input.Label))
            Input.Label = feasibility.Title;
        if (!Input.ProjectId.HasValue)
            Input.ProjectId = feasibility.ProjectId;
        if (string.IsNullOrWhiteSpace(Input.Notes))
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(feasibility.SiteAddress))
                lines.Add($"Direccion en sitio: {feasibility.SiteAddress}");
            if (!string.IsNullOrWhiteSpace(feasibility.Coordinates))
                lines.Add($"Coordenadas: {feasibility.Coordinates}");
            if (!string.IsNullOrWhiteSpace(feasibility.SiteContactName) || !string.IsNullOrWhiteSpace(feasibility.SiteContactPhone))
                lines.Add($"Contacto sitio: {feasibility.SiteContactName} {feasibility.SiteContactPhone}".Trim());
            var baseNotes = StripNotesMetadata(feasibility.Notes ?? "");
            if (!string.IsNullOrWhiteSpace(baseNotes))
                lines.Add(baseNotes);
            Input.Notes = string.Join(Environment.NewLine, lines);
        }
    }

    private static string ComposeFeasibilityMetadata(string rawNotes, Guid? feasibilityId)
    {
        if (!feasibilityId.HasValue)
            return rawNotes;

        var cleaned = rawNotes
            .Split('\n')
            .Select(x => x.TrimEnd('\r'))
            .Where(x => !x.StartsWith("[META] FactibilidadId=", StringComparison.OrdinalIgnoreCase));
        var baseNotes = string.Join(Environment.NewLine, cleaned).Trim();
        if (string.IsNullOrWhiteSpace(baseNotes))
            return $"[META] FactibilidadId={feasibilityId.Value}";
        return $"{baseNotes}{Environment.NewLine}[META] FactibilidadId={feasibilityId.Value}";
    }

    private static string StripNotesMetadata(string raw)
    {
        var lines = (raw ?? "").Split('\n')
            .Select(x => x.TrimEnd('\r'))
            .Where(x => !x.StartsWith("[META]", StringComparison.OrdinalIgnoreCase));
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string NormalizeRecurrence(string? recurrence) => (recurrence ?? "").Trim() switch
    {
        "Unica" => "Unica",
        "Anual" => "Anual",
        _ => "Mensual"
    };

    private static string NormalizeTerm(string? termOption)
    {
        var value = (termOption ?? "").Trim();
        if (value.Equals("Indefinido", StringComparison.OrdinalIgnoreCase))
            return "Indefinido";

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var months) && months > 0
            ? months.ToString(CultureInfo.InvariantCulture)
            : "Indefinido";
    }
}


