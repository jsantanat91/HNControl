using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services.Monitoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Monitoring;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IMonitorProbeService _probe;
    public IndexModel(ApplicationDbContext db, IMonitorProbeService probe)
    {
        _db = db;
        _probe = probe;
    }

    public bool IsAdmin { get; private set; }

    public List<ClientGroupVm> Groups { get; private set; } = new();
    public int UpCount { get; private set; }
    public int DownCount { get; private set; }
    public int UnknownCount { get; private set; }

    public async Task OnGetAsync()
    {
        IsAdmin = User.IsInRole(AppRoles.Admin);

        var targets = await _db.MonitorTargets
            .Include(t => t.Client)
            .Include(t => t.ClientServiceContract)
            .Include(t => t.ClientCarrierService)
            .OrderBy(t => t.Client!.Name)
            .ThenBy(t => t.Name)
            .ToListAsync();

        UpCount = targets.Count(t => t.LastStatus == MonitorStatus.Up);
        DownCount = targets.Count(t => t.LastStatus == MonitorStatus.Down);
        UnknownCount = targets.Count(t => t.LastStatus == MonitorStatus.Unknown);

        Groups = targets
            .GroupBy(t => t.ClientId)
            .Select(g => new ClientGroupVm
            {
                ClientId = g.Key,
                ClientName = g.First().Client?.Name ?? "(Sin cliente)",
                Items = g.Select(t => new TargetVm
                {
                    Id = t.Id,
                    Name = t.Name,
                    ContractLabel = t.ClientServiceContract != null ? t.ClientServiceContract.Label : "",
                    CarrierServiceLabel = t.ClientCarrierService != null ? t.ClientCarrierService.ServiceLabel : "",
                    ProbeType = t.ProbeType,
                    Address = !string.IsNullOrWhiteSpace(t.IpAddress) ? t.IpAddress : t.Fqdn,
                    LastStatus = t.LastStatus,
                    LastCheckedAt = t.LastCheckedAt,
                    LastLatencyMs = t.LastLatencyMs,
                    LastError = t.LastError,
                    IntervalSeconds = t.CheckIntervalSeconds,
                    IsActive = t.IsActive
                }).ToList()
            })
            .OrderBy(x => x.ClientName)
            .ToList();
    }

    public async Task<IActionResult> OnPostRunAsync(Guid id)
    {
        if (!User.IsInRole(AppRoles.Admin))
            return Forbid();

        var t = await _db.MonitorTargets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        var now = DateTime.UtcNow;
        var res = await _probe.ProbeAsync(t);

        _db.MonitorChecks.Add(new MonitorCheck
        {
            TargetId = t.Id,
            CheckedAt = now,
            Success = res.Success,
            LatencyMs = res.LatencyMs,
            Error = res.Error ?? ""
        });

        t.LastCheckedAt = now;
        t.LastLatencyMs = res.LatencyMs;
        t.LastError = res.Success ? "" : (res.Error ?? "");

        if (res.Success)
        {
            t.ConsecutiveFails = 0;
            t.LastStatus = MonitorStatus.Up;
        }
        else
        {
            t.ConsecutiveFails += 1;
            if (t.ConsecutiveFails >= Math.Max(1, t.FailThreshold))
                t.LastStatus = MonitorStatus.Down;
        }

        t.NextCheckAt = now.AddSeconds(Math.Max(10, t.CheckIntervalSeconds));
        t.UpdatedAt = now;

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public class ClientGroupVm
    {
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = "";
        public List<TargetVm> Items { get; set; } = new();
    }

    public class TargetVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string ContractLabel { get; set; } = "";
        public string CarrierServiceLabel { get; set; } = "";
        public MonitorProbeType ProbeType { get; set; }
        public string Address { get; set; } = "";
        public MonitorStatus LastStatus { get; set; }
        public DateTime? LastCheckedAt { get; set; }
        public int? LastLatencyMs { get; set; }
        public string LastError { get; set; } = "";
        public int IntervalSeconds { get; set; }
        public bool IsActive { get; set; }

        public string BadgeClass => LastStatus switch
        {
            MonitorStatus.Up => "text-bg-success",
            MonitorStatus.Down => "text-bg-danger",
            _ => "text-bg-secondary"
        };
    }
}
