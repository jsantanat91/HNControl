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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    public List<SelectListItem> ClientOptions { get; set; } = new();
    public List<SelectListItem> CarrierOptions { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadListsAsync();

        var svc = await _db.ClientCarrierServices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (svc == null) return NotFound();

        Input = new InputModel
        {
            Id = svc.Id,
            ClientId = svc.ClientId,
            CarrierId = svc.CarrierId,
            ServiceLabel = svc.ServiceLabel,
            Plan = svc.Plan,
            AccountNumber = svc.AccountNumber,
            ContractNumber = svc.ContractNumber,
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
        await LoadListsAsync();
        if (!ModelState.IsValid) return Page();

        var svc = await _db.ClientCarrierServices.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (svc == null) return NotFound();

        svc.ClientId = Input.ClientId;
        svc.CarrierId = Input.CarrierId;
        svc.ServiceLabel = Input.ServiceLabel.Trim();
        svc.Plan = (Input.Plan ?? "").Trim();
        svc.AccountNumber = (Input.AccountNumber ?? "").Trim();
        svc.ContractNumber = (Input.ContractNumber ?? "").Trim();
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

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ClientOptions = clients.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();

        var carriers = await _db.InternetCarriers.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        CarrierOptions = carriers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
    }
}
