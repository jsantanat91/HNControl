using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Carriers.Services;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public List<SelectListItem> ClientOptions { get; set; } = new();
    public List<SelectListItem> CarrierOptions { get; set; } = new();
    public List<SelectListItem> ContractOptions { get; set; } = new();
    public string ContractMapJson { get; set; } = "{}";

    [BindProperty(SupportsGet = true)]
    public Guid? ClientId { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid CarrierId { get; set; }

        public Guid? ClientServiceContractId { get; set; }

        [Required, MaxLength(140)]
        public string ServiceLabel { get; set; } = "";

        [MaxLength(40)] public string? ServiceType { get; set; }
        [MaxLength(140)] public string? Plan { get; set; }
        [MaxLength(140)] public string? PlanOther { get; set; }
        [MaxLength(120)] public string? AccountNumber { get; set; }
        [MaxLength(120)] public string? ContractNumber { get; set; }
        [MaxLength(180)] public string? BusinessName { get; set; }
        [MaxLength(120)] public string? SerialNumber { get; set; }
        [MaxLength(120)] public string? CircuitId { get; set; }
        [MaxLength(200)] public string? ServiceAddress { get; set; }
        [MaxLength(200)] public string? IpInfo { get; set; }
        [MaxLength(120)] public string? Gateway { get; set; }
        [MaxLength(120)] public string? GatewayLink { get; set; }
        [MaxLength(180)] public string? Fqdn { get; set; }
        [MaxLength(40)] public string? SupportPhoneOverride { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadListsAsync();
        if (ClientId.HasValue) Input.ClientId = ClientId.Value;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (!ModelState.IsValid) return Page();

        if (Input.ClientServiceContractId.HasValue)
        {
            var contract = await _db.ClientServiceContracts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == Input.ClientServiceContractId.Value && c.ClientId == Input.ClientId);

            if (contract != null)
            {
                if (string.IsNullOrWhiteSpace(Input.ServiceLabel)) Input.ServiceLabel = contract.Label;
                if (string.IsNullOrWhiteSpace(Input.AccountNumber)) Input.AccountNumber = contract.AccountNumber;
                if (string.IsNullOrWhiteSpace(Input.ContractNumber)) Input.ContractNumber = contract.ContractNumber;
                Input.ServiceAddress = contract.BranchAddress;

                if (Input.CarrierId == Guid.Empty && !string.IsNullOrWhiteSpace(contract.Provider))
                {
                    var provider = contract.Provider.Trim().ToLower();
                    var carrier = await _db.InternetCarriers
                        .AsNoTracking()
                        .Where(c => c.IsActive)
                        .FirstOrDefaultAsync(c => c.Name.ToLower().Contains(provider));
                    if (carrier != null) Input.CarrierId = carrier.Id;
                }
            }
        }

        var svc = new ClientCarrierService
        {
            ClientId = Input.ClientId,
            CarrierId = Input.CarrierId,
            ClientServiceContractId = Input.ClientServiceContractId,
            ServiceLabel = Input.ServiceLabel.Trim(),
            ServiceType = (Input.ServiceType ?? "").Trim(),
            Plan = ResolvePlan(Input.Plan, Input.PlanOther),
            AccountNumber = (Input.AccountNumber ?? "").Trim(),
            ContractNumber = (Input.ContractNumber ?? "").Trim(),
            BusinessName = (Input.BusinessName ?? "").Trim(),
            SerialNumber = (Input.SerialNumber ?? "").Trim(),
            CircuitId = (Input.CircuitId ?? "").Trim(),
            ServiceAddress = (Input.ServiceAddress ?? "").Trim(),
            IpInfo = (Input.IpInfo ?? "").Trim(),
            Gateway = (Input.Gateway ?? "").Trim(),
            GatewayLink = (Input.GatewayLink ?? "").Trim(),
            Fqdn = (Input.Fqdn ?? "").Trim(),
            SupportPhoneOverride = (Input.SupportPhoneOverride ?? "").Trim(),
            IsActive = Input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClientCarrierServices.Add(svc);
        await _db.SaveChangesAsync();

        return RedirectToPage("./Index", new { clientId = svc.ClientId });
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ClientOptions = clients
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToList();

        var carriers = await _db.InternetCarriers.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        CarrierOptions = carriers
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToList();

        ContractOptions = new();
        if (Input.ClientId != Guid.Empty || ClientId.HasValue)
        {
            var currentClientId = Input.ClientId != Guid.Empty ? Input.ClientId : ClientId!.Value;
            var contracts = await _db.ClientServiceContracts
                .AsNoTracking()
                .Where(x => x.ClientId == currentClientId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ContractOptions = contracts
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(c.Branch)
                        ? c.Label
                        : $"{c.Label} - {c.Branch.Trim()}"
                })
                .ToList();

            var map = contracts.ToDictionary(
                c => c.Id.ToString(),
                c => new
                {
                    label = c.Label,
                    provider = c.Provider,
                    accountNumber = c.AccountNumber,
                    contractNumber = c.ContractNumber,
                    branchAddress = c.BranchAddress
                });
            ContractMapJson = JsonSerializer.Serialize(map);
        }
    }

    private static string ResolvePlan(string? selectedPlan, string? otherPlan)
    {
        var value = (selectedPlan ?? "").Trim();
        if (value.Equals("Otro", StringComparison.OrdinalIgnoreCase))
            return (otherPlan ?? "").Trim();
        return value;
    }
}
