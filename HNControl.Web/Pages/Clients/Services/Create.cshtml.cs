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
public class CreateModel : PageModel
{
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

    public string ClientName { get; set; } = "";
    public string ClientCode { get; set; } = "";

    public SelectList ServiceTypeItems =>
        new(Enum.GetValues<ClientServiceType>().Select(x => new { Id = x, Name = x.ToString() }), "Id", "Name");

    public SelectList ProjectItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public ClientServiceType ServiceType { get; set; } = ClientServiceType.Internet;

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

        if (!ModelState.IsValid) return Page();

        var contract = new ClientServiceContract
        {
            ClientId = ClientId,
            ProjectId = Input.ProjectId,
            ServiceType = Input.ServiceType,
            Label = (Input.Label ?? "").Trim(),
            Provider = (Input.Provider ?? "").Trim(),
            AccountNumber = string.IsNullOrWhiteSpace(Input.AccountNumber) ? client.ClientCode : Input.AccountNumber.Trim(),
            ContractNumber = string.IsNullOrWhiteSpace(Input.ContractNumber)
                ? $"{client.ClientCode}-{await _db.ClientServiceContracts.CountAsync(c => c.ClientId == ClientId) + 1:00}"
                : Input.ContractNumber.Trim(),
            PortalUrl = (Input.PortalUrl ?? "").Trim(),
            PortalUsername = (Input.PortalUsername ?? "").Trim(),
            PortalPasswordProtected = _protector.Protect((Input.PortalPassword ?? "").Trim()),
            ContractStartDate = Input.ContractStartDate?.Date,
            ContractEndDate = Input.ContractEndDate?.Date,
            Notes = (Input.Notes ?? "").Trim(),
            MonthlyAmount = Input.MonthlyAmount,
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
}
