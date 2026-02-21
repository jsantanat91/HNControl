using System.ComponentModel.DataAnnotations;
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

        [Required, MaxLength(140)]
        public string ServiceLabel { get; set; } = "";

        [MaxLength(140)] public string? Plan { get; set; }
        [MaxLength(120)] public string? AccountNumber { get; set; }
        [MaxLength(120)] public string? ContractNumber { get; set; }
        [MaxLength(120)] public string? CircuitId { get; set; }
        [MaxLength(200)] public string? ServiceAddress { get; set; }
        [MaxLength(200)] public string? IpInfo { get; set; }
        [MaxLength(40)] public string? SupportPhoneOverride { get; set; }
        [MaxLength(2000)] public string? Notes { get; set; }
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

        var svc = new ClientCarrierService
        {
            ClientId = Input.ClientId,
            CarrierId = Input.CarrierId,
            ServiceLabel = Input.ServiceLabel.Trim(),
            Plan = (Input.Plan ?? "").Trim(),
            AccountNumber = (Input.AccountNumber ?? "").Trim(),
            ContractNumber = (Input.ContractNumber ?? "").Trim(),
            CircuitId = (Input.CircuitId ?? "").Trim(),
            ServiceAddress = (Input.ServiceAddress ?? "").Trim(),
            IpInfo = (Input.IpInfo ?? "").Trim(),
            SupportPhoneOverride = (Input.SupportPhoneOverride ?? "").Trim(),
            Notes = (Input.Notes ?? "").Trim(),
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
    }
}
