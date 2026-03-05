using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Monitoring.Targets;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public EditModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Clients { get; private set; } = new();
    public List<SelectListItem> Contracts { get; private set; } = new();
    public List<SelectListItem> CarrierServices { get; private set; } = new();
    public string CarrierServiceMapJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid id, Guid? clientId = null)
    {
        var t = await _db.MonitorTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return NotFound();

        var selectedClientId = clientId ?? t.ClientId;
        await LoadListsAsync(selectedClientId);

        Input = new InputModel
        {
            Id = t.Id,
            ClientId = selectedClientId,
            ClientServiceContractId = t.ClientServiceContractId ?? Guid.Empty,
            ClientCarrierServiceId = t.ClientCarrierServiceId ?? Guid.Empty,
            Name = t.Name,
            Fqdn = t.Fqdn,
            IpAddress = t.IpAddress,
            SubnetMask = t.SubnetMask,
            Gateway = t.Gateway,
            ProbeType = t.ProbeType,
            TcpPort = t.TcpPort,
            HttpUrl = t.HttpUrl,
            CheckIntervalSeconds = t.CheckIntervalSeconds,
            TimeoutMs = t.TimeoutMs,
            FailThreshold = t.FailThreshold,
            Notes = t.Notes,
            IsActive = t.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync(Input.ClientId);

        if (!ModelState.IsValid)
            return Page();

        if (Input.ClientCarrierServiceId != Guid.Empty)
        {
            var svc = await _db.ClientCarrierServices
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == Input.ClientCarrierServiceId && s.ClientId == Input.ClientId);
            if (svc != null)
            {
                if (Input.ClientServiceContractId == Guid.Empty && svc.ClientServiceContractId.HasValue)
                    Input.ClientServiceContractId = svc.ClientServiceContractId.Value;
                if (string.IsNullOrWhiteSpace(Input.Name)) Input.Name = svc.ServiceLabel;
                if (string.IsNullOrWhiteSpace(Input.IpAddress)) Input.IpAddress = svc.IpInfo;
            }
        }

        var t = await _db.MonitorTargets.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (t == null) return NotFound();

        var now = DateTime.UtcNow;

        t.ClientId = Input.ClientId;
        t.ClientServiceContractId = Input.ClientServiceContractId == Guid.Empty ? null : Input.ClientServiceContractId;
        t.ClientCarrierServiceId = Input.ClientCarrierServiceId == Guid.Empty ? null : Input.ClientCarrierServiceId;
        t.Name = Input.Name.Trim();
        t.Fqdn = (Input.Fqdn ?? "").Trim();
        t.IpAddress = (Input.IpAddress ?? "").Trim();
        t.SubnetMask = (Input.SubnetMask ?? "").Trim();
        t.Gateway = (Input.Gateway ?? "").Trim();
        t.ProbeType = Input.ProbeType;
        t.TcpPort = Input.TcpPort;
        t.HttpUrl = (Input.HttpUrl ?? "").Trim();
        t.CheckIntervalSeconds = Math.Clamp(Input.CheckIntervalSeconds, 10, 86400);
        t.TimeoutMs = Math.Clamp(Input.TimeoutMs, 250, 60000);
        t.FailThreshold = Math.Clamp(Input.FailThreshold, 1, 50);
        t.IsActive = Input.IsActive;
        t.Notes = (Input.Notes ?? "").Trim();

        if (t.IsActive && (t.NextCheckAt == null || t.NextCheckAt > now))
            t.NextCheckAt = now;

        t.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task LoadListsAsync(Guid clientId)
    {
        Clients = await _db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        Contracts = await _db.ClientServiceContracts
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SelectListItem(x.Label, x.Id.ToString()))
            .ToListAsync();

        var services = await _db.ClientCarrierServices
            .AsNoTracking()
            .Include(x => x.Carrier)
            .Where(x => x.ClientId == clientId && x.IsActive)
            .OrderBy(x => x.ServiceLabel)
            .ToListAsync();

        CarrierServices = services
            .Select(s => new SelectListItem($"{s.ServiceLabel} · {(s.Carrier != null ? s.Carrier.Name : "Carrier")}", s.Id.ToString()))
            .ToList();

        var map = services.ToDictionary(
            s => s.Id.ToString(),
            s => new
            {
                name = s.ServiceLabel,
                ipInfo = s.IpInfo,
                contractId = s.ClientServiceContractId?.ToString() ?? "",
                notes = s.Notes
            });
        CarrierServiceMapJson = JsonSerializer.Serialize(map);
    }

    public class InputModel
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        public Guid ClientServiceContractId { get; set; }
        public Guid ClientCarrierServiceId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(255)]
        public string? Fqdn { get; set; }

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(32)]
        public string? SubnetMask { get; set; }

        [MaxLength(64)]
        public string? Gateway { get; set; }

        public MonitorProbeType ProbeType { get; set; } = MonitorProbeType.IcmpPing;

        public int? TcpPort { get; set; }
        public string? HttpUrl { get; set; }

        public int CheckIntervalSeconds { get; set; } = 60;
        public int TimeoutMs { get; set; } = 1500;
        public int FailThreshold { get; set; } = 3;

        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
    }
}
