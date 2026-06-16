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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ISecretProtector _protector;

    public EditModel(ApplicationDbContext db, IFileStorage storage, ISecretProtector protector)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
    }

    public ClientServiceContract? Contract { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string ClientCode { get; set; } = "";

    public bool HasContractFile => Contract != null && !string.IsNullOrWhiteSpace(Contract.SignedContractStoragePath);
    public string ContractFileName => Contract?.SignedContractOriginalFileName ?? "contrato.pdf";

    public SelectList ServiceTypeItems =>
        new(Enum.GetValues<ClientServiceType>().Select(x => new { Id = x, Name = x.ToString() }), "Id", "Name");

    public SelectList ProjectItems { get; set; } = default!;
    public SelectList SalesOpportunityItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }

        [Required] public ClientServiceType ServiceType { get; set; }

        public List<string> SelectedServiceTypes { get; set; } = [];

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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Contract = await _db.ClientServiceContracts
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Contract == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Contract.Client?.ClientCode))
        {
            Contract.Client!.ClientCode = await NextClientCodeAsync();
            await _db.SaveChangesAsync();
        }

        ClientId = Contract.ClientId;
        ClientName = Contract.Client?.Name ?? "";
        ClientCode = Contract.Client?.ClientCode ?? "";

        await LoadProjectsAsync(ClientId);
        await LoadSalesOpportunitiesAsync(ClientId);
        var meta = ParseNotesMetadata(Contract.Notes);
        var tech = ClientServiceContractMetadata.ParseTechnical(Contract.Notes);

        Input = new InputModel
        {
            Id = Contract.Id,
            ServiceType = Contract.ServiceType,
            SelectedServiceTypes = tech.ServiceTypes.Any() ? tech.ServiceTypes.ToList() : [Contract.ServiceType.ToString()],
            Label = Contract.Label,
            Provider = Contract.Provider,
            AccountNumber = string.IsNullOrWhiteSpace(Contract.AccountNumber) ? ClientCode : Contract.AccountNumber,
            ContractNumber = string.IsNullOrWhiteSpace(Contract.ContractNumber) ? $"{ClientCode}-01" : Contract.ContractNumber,
            MonthlyAmount = Contract.MonthlyAmount,
            Branch = Contract.Branch,
            BranchAddress = Contract.BranchAddress,
            BillingRecurrence = meta.Recurrence,
            ContractTermOption = meta.Term,
            InstallationCost = meta.InstallationCost,
            InternetCapacity = tech.InternetCapacity,
            InternetCapacityOther = tech.InternetCapacityOther,
            TelephonyExtensions = tech.TelephonyExtensions,
            TelephonyTrunks = tech.TelephonyTrunks,
            TelephonyDids = tech.TelephonyDids,
            CctvChannels = tech.CctvChannels,
            CctvChannelsOther = tech.CctvChannelsOther,
            SecurityBrand = tech.SecurityBrand,
            SecurityBrandOther = tech.SecurityBrandOther,
            ServerOs = tech.ServerOs,
            ServerCpuCores = tech.ServerCpuCores,
            ServerRam = tech.ServerRam,
            ServerDisk = tech.ServerDisk,
            SalesOpportunityId = meta.SalesOpportunityId,
            ContractStartDate = Contract.ContractStartDate?.Date,
            ContractEndDate = Contract.ContractEndDate?.Date,
            ProjectId = Contract.ProjectId,
            Notes = meta.Notes
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Contract = await _db.ClientServiceContracts
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == Input.Id);

        if (Contract == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Contract.Client?.ClientCode))
        {
            Contract.Client!.ClientCode = await NextClientCodeAsync();
            await _db.SaveChangesAsync();
        }

        ClientId = Contract.ClientId;
        ClientName = Contract.Client?.Name ?? "";
        ClientCode = Contract.Client?.ClientCode ?? "";

        await LoadProjectsAsync(ClientId);
        await LoadSalesOpportunitiesAsync(ClientId);

        if (!ModelState.IsValid) return Page();

        NormalizeSelectedServiceTypes();

        Contract.ServiceType = PrimaryServiceType(Input.SelectedServiceTypes, Input.ServiceType);
        Contract.Label = (Input.Label ?? "").Trim();
        Contract.Provider = (Input.Provider ?? "").Trim();
        Contract.AccountNumber = string.IsNullOrWhiteSpace(Input.AccountNumber) ? ClientCode : Input.AccountNumber.Trim();
        Contract.ContractNumber = string.IsNullOrWhiteSpace(Input.ContractNumber) ? $"{ClientCode}-01" : Input.ContractNumber.Trim();
        Contract.MonthlyAmount = Input.MonthlyAmount;
        Contract.Branch = (Input.Branch ?? "").Trim();
        Contract.BranchAddress = (Input.BranchAddress ?? "").Trim();
        Contract.ContractStartDate = Input.ContractStartDate?.Date;
        Contract.ContractEndDate = Input.ContractEndDate?.Date;
        Contract.ProjectId = Input.ProjectId;
        Contract.Notes = ComposeNotesMetadata(
            (Input.Notes ?? "").Trim(),
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
            Input.ServerDisk);
        Contract.UpdatedAt = DateTime.UtcNow;

        if (Input.SignedContract != null && Input.SignedContract.Length > 0)
        {
            try
            {
                var (path, size, contentType, original) = await _storage.SaveFileAsync(
                    Input.SignedContract,
                    subFolder: $"client-contracts/{ClientId}",
                    fileNameNoExt: $"contract_{Contract.Id}",
                    allowedExtensions: new[] { ".pdf" },
                    maxBytes: 20L * 1024L * 1024L);

                Contract.SignedContractStoragePath = path;
                Contract.SignedContractSizeBytes = size;
                Contract.SignedContractContentType = contentType;
                Contract.SignedContractOriginalFileName = original;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                return Page();
            }
        }

        await _db.SaveChangesAsync();
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

    private async Task LoadProjectsAsync(Guid clientId)
    {
        var projs = await _db.Projects
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new { p.Id, p.Title })
            .ToListAsync();

        ProjectItems = new SelectList(projs, "Id", "Title");
    }

    private async Task LoadSalesOpportunitiesAsync(Guid clientId)
    {
        var rows = await _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Where(x => x.ClientId == clientId)
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

    private static (string Notes, string Recurrence, string Term, Guid? SalesOpportunityId, decimal InstallationCost) ParseNotesMetadata(string? rawNotes)
    {
        var notes = new List<string>();
        string recurrence = "Mensual";
        string term = "12";
        Guid? salesOpportunityId = null;
        var installationCost = 0m;

        foreach (var line in (rawNotes ?? string.Empty).Split('\n'))
        {
            var clean = line.Trim().TrimEnd('\r');
            if (!clean.StartsWith("[META]", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(clean))
                    notes.Add(clean);
                continue;
            }

            var payload = clean.Substring(6).Trim();
            var parts = payload.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            var key = parts[0];
            var value = parts[1];

            if (key.Equals("Recurrencia", StringComparison.OrdinalIgnoreCase))
                recurrence = NormalizeRecurrence(value);
            else if (key.Equals("Plazo", StringComparison.OrdinalIgnoreCase))
                term = NormalizeTerm(value);
            else if (key.Equals("CostoInstalacion", StringComparison.OrdinalIgnoreCase) || key.Equals("COSTOINST", StringComparison.OrdinalIgnoreCase))
                installationCost = ParseMoney(value);
            else if (key.Equals("VentaId", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(value, out var gid))
                salesOpportunityId = gid;
        }

        return (string.Join(Environment.NewLine, notes).Trim(), recurrence, term, salesOpportunityId, installationCost);
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
        var notes = (rawNotes ?? string.Empty).Trim();
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
            Input.SelectedServiceTypes = [Input.ServiceType.ToString()];
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

    private static decimal ParseMoney(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant))
            return Math.Max(0m, invariant);
        if (decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("es-MX"), out var localized))
            return Math.Max(0m, localized);
        return 0m;
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


