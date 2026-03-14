using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Monitoring;

public class CreateTargetModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateTargetModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Clients { get; private set; } = new();
    public List<SelectListItem> Contracts { get; private set; } = new();
    public List<SelectListItem> CarrierServices { get; private set; } = new();
    public string CarrierServiceMapJson { get; private set; } = "{}";

    public async Task OnGetAsync(Guid? clientId = null)
    {
        await LoadListsAsync(clientId);
        if (clientId != null)
            Input.ClientId = clientId.Value;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync(Input.ClientId == Guid.Empty ? null : Input.ClientId);

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
                // Estos campos siempre se sincronizan desde el servicio carrier seleccionado.
                Input.IpAddress = svc.IpInfo;
                Input.SubnetMask = svc.Gateway;
                Input.Gateway = svc.GatewayLink;
            }
        }

        var now = DateTime.UtcNow;
        var target = new MonitorTarget
        {
            ClientId = Input.ClientId,
            ClientServiceContractId = Input.ClientServiceContractId == Guid.Empty ? null : Input.ClientServiceContractId,
            ClientCarrierServiceId = Input.ClientCarrierServiceId == Guid.Empty ? null : Input.ClientCarrierServiceId,
            Name = Input.Name.Trim(),
            Fqdn = (Input.Fqdn ?? "").Trim(),
            IpAddress = (Input.IpAddress ?? "").Trim(),
            SubnetMask = (Input.SubnetMask ?? "").Trim(),
            Gateway = (Input.Gateway ?? "").Trim(),
            ProbeType = Input.ProbeType,
            TcpPort = Input.TcpPort,
            HttpUrl = (Input.HttpUrl ?? "").Trim(),
            CheckIntervalSeconds = Math.Clamp(Input.CheckIntervalSeconds, 10, 86400),
            TimeoutMs = Math.Clamp(Input.TimeoutMs, 250, 60000),
            FailThreshold = Math.Clamp(Input.FailThreshold, 1, 50),
            IsActive = true,
            NextCheckAt = now,
            Notes = (Input.Notes ?? "").Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.MonitorTargets.Add(target);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Monitoring/Index");
    }

    private async Task LoadListsAsync(Guid? clientId)
    {
        Clients = await _db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        Contracts = new List<SelectListItem>();
        CarrierServices = new List<SelectListItem>();

        if (clientId != null && clientId != Guid.Empty)
        {
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
                    subnetMask = s.Gateway,
                    gateway = s.GatewayLink,
                    contractId = s.ClientServiceContractId?.ToString() ?? "",
                    notes = s.Notes
                });
            CarrierServiceMapJson = JsonSerializer.Serialize(map);
        }
    }

    public class InputModel
    {
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

        public string? Notes { get; set; }
    }
}
