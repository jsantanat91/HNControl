using System.ComponentModel.DataAnnotations;
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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var t = await _db.MonitorTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return NotFound();

        await LoadListsAsync(t.ClientId);

        Input = new InputModel
        {
            Id = t.Id,
            ClientId = t.ClientId,
            ClientServiceContractId = t.ClientServiceContractId ?? Guid.Empty,
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

        var t = await _db.MonitorTargets.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (t == null) return NotFound();

        var now = DateTime.UtcNow;

        t.ClientId = Input.ClientId;
        t.ClientServiceContractId = Input.ClientServiceContractId == Guid.Empty ? null : Input.ClientServiceContractId;
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

        // si se reactivó, programamos check inmediato
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
    }

    public class InputModel
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        public Guid ClientServiceContractId { get; set; }

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
