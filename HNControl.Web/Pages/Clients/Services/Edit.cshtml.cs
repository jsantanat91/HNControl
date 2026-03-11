using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients.Services;

[Authorize(Roles = AppRoles.Admin)]
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

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }

        [Required] public ClientServiceType ServiceType { get; set; }

        [Required, MaxLength(200)]
        public string Label { get; set; } = "";

        [MaxLength(120)]
        public string Provider { get; set; } = "";

        [MaxLength(120)]
        public string AccountNumber { get; set; } = "";

        [MaxLength(120)]
        public string ContractNumber { get; set; } = "";

        [MaxLength(300)]
        public string PortalUrl { get; set; } = "";

        [MaxLength(200)]
        public string PortalUsername { get; set; } = "";

        [MaxLength(300)]
        public string PortalPassword { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime? ContractStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ContractEndDate { get; set; }

        public Guid? ProjectId { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Range(0, 99999999)]
        public decimal? MonthlyAmount { get; set; }

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

        Input = new InputModel
        {
            Id = Contract.Id,
            ServiceType = Contract.ServiceType,
            Label = Contract.Label,
            Provider = Contract.Provider,
            AccountNumber = string.IsNullOrWhiteSpace(Contract.AccountNumber) ? ClientCode : Contract.AccountNumber,
            ContractNumber = string.IsNullOrWhiteSpace(Contract.ContractNumber) ? $"{ClientCode}-01" : Contract.ContractNumber,
            MonthlyAmount = Contract.MonthlyAmount,
            Branch = Contract.Branch,
            BranchAddress = Contract.BranchAddress,
            PortalUrl = Contract.PortalUrl ?? "",
            PortalUsername = Contract.PortalUsername ?? "",
            PortalPassword = _protector.Unprotect(Contract.PortalPasswordProtected),
            ContractStartDate = Contract.ContractStartDate?.Date,
            ContractEndDate = Contract.ContractEndDate?.Date,
            ProjectId = Contract.ProjectId,
            Notes = Contract.Notes
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

        if (!ModelState.IsValid) return Page();

        Contract.ServiceType = Input.ServiceType;
        Contract.Label = (Input.Label ?? "").Trim();
        Contract.Provider = (Input.Provider ?? "").Trim();
        Contract.AccountNumber = string.IsNullOrWhiteSpace(Input.AccountNumber) ? ClientCode : Input.AccountNumber.Trim();
        Contract.ContractNumber = string.IsNullOrWhiteSpace(Input.ContractNumber) ? $"{ClientCode}-01" : Input.ContractNumber.Trim();
        Contract.MonthlyAmount = Input.MonthlyAmount;
        Contract.Branch = (Input.Branch ?? "").Trim();
        Contract.BranchAddress = (Input.BranchAddress ?? "").Trim();
        Contract.PortalUrl = (Input.PortalUrl ?? "").Trim();
        Contract.PortalUsername = (Input.PortalUsername ?? "").Trim();
        Contract.PortalPasswordProtected = _protector.Protect((Input.PortalPassword ?? "").Trim());
        Contract.ContractStartDate = Input.ContractStartDate?.Date;
        Contract.ContractEndDate = Input.ContractEndDate?.Date;
        Contract.ProjectId = Input.ProjectId;
        Contract.Notes = (Input.Notes ?? "").Trim();
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
}
