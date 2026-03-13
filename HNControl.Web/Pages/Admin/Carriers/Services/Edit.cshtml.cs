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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public Guid? ClientId { get; set; }

    public List<SelectListItem> ClientOptions { get; set; } = new();
    public List<SelectListItem> CarrierOptions { get; set; } = new();
    public List<SelectListItem> ContractOptions { get; set; } = new();
    public string ContractMapJson { get; set; } = "{}";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid CarrierId { get; set; }

        public Guid? ClientServiceContractId { get; set; }

        [Required, MaxLength(140)]
        public string ServiceLabel { get; set; } = "";

        [MaxLength(140)] public string? Plan { get; set; }
        [MaxLength(120)] public string? AccountNumber { get; set; }
        [MaxLength(120)] public string? ContractNumber { get; set; }
        [MaxLength(180)] public string? BusinessName { get; set; }
        [MaxLength(120)] public string? SerialNumber { get; set; }
        [MaxLength(120)] public string? CircuitId { get; set; }
        [MaxLength(200)] public string? ServiceAddress { get; set; }
        [MaxLength(200)] public string? IpInfo { get; set; }
        [MaxLength(40)] public string? SupportPhoneOverride { get; set; }
        [MaxLength(2000)] public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var svc = await _db.ClientCarrierServices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (svc == null) return NotFound();

        var loadClientId = ClientId.HasValue && ClientId.Value != Guid.Empty ? ClientId.Value : svc.ClientId;
        await LoadListsAsync(loadClientId);

        Input = new InputModel
        {
            Id = svc.Id,
            ClientId = loadClientId,
            CarrierId = svc.CarrierId,
            ClientServiceContractId = svc.ClientServiceContractId,
            ServiceLabel = svc.ServiceLabel,
            Plan = svc.Plan,
            AccountNumber = svc.AccountNumber,
            ContractNumber = svc.ContractNumber,
            BusinessName = svc.BusinessName,
            SerialNumber = svc.SerialNumber,
            CircuitId = svc.CircuitId,
            ServiceAddress = svc.ServiceAddress,
            IpInfo = svc.IpInfo,
            SupportPhoneOverride = svc.SupportPhoneOverride,
            Notes = svc.Notes,
            IsActive = svc.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync(Input.ClientId);
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
            }
        }

        var svc = await _db.ClientCarrierServices.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (svc == null) return NotFound();

        svc.ClientId = Input.ClientId;
        svc.CarrierId = Input.CarrierId;
        svc.ClientServiceContractId = Input.ClientServiceContractId;
        svc.ServiceLabel = Input.ServiceLabel.Trim();
        svc.Plan = (Input.Plan ?? "").Trim();
        svc.AccountNumber = (Input.AccountNumber ?? "").Trim();
        svc.ContractNumber = (Input.ContractNumber ?? "").Trim();
        svc.BusinessName = (Input.BusinessName ?? "").Trim();
        svc.SerialNumber = (Input.SerialNumber ?? "").Trim();
        svc.CircuitId = (Input.CircuitId ?? "").Trim();
        svc.ServiceAddress = (Input.ServiceAddress ?? "").Trim();
        svc.IpInfo = (Input.IpInfo ?? "").Trim();
        svc.SupportPhoneOverride = (Input.SupportPhoneOverride ?? "").Trim();
        svc.Notes = (Input.Notes ?? "").Trim();
        svc.IsActive = Input.IsActive;
        svc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index", new { clientId = svc.ClientId });
    }

    private async Task LoadListsAsync(Guid? clientId = null)
    {
        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ClientOptions = clients.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();

        var carriers = await _db.InternetCarriers.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        CarrierOptions = carriers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();

        ContractOptions = new();
        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            var contracts = await _db.ClientServiceContracts
                .AsNoTracking()
                .Where(x => x.ClientId == clientId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ContractOptions = contracts
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Label })
                .ToList();

            var map = contracts.ToDictionary(
                c => c.Id.ToString(),
                c => new
                {
                    label = c.Label,
                    provider = c.Provider,
                    accountNumber = c.AccountNumber,
                    contractNumber = c.ContractNumber
                });
            ContractMapJson = JsonSerializer.Serialize(map);
        }
    }
}
