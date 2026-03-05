using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Monitoring.Targets;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Row> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var targets = await _db.MonitorTargets
            .Include(t => t.Client)
            .Include(t => t.ClientServiceContract)
            .Include(t => t.ClientCarrierService)
            .OrderBy(t => t.Client!.Name)
            .ThenBy(t => t.Name)
            .ToListAsync();

        Items = targets.Select(t => new Row
        {
            Id = t.Id,
            Client = t.Client?.Name ?? "(Sin cliente)",
            Name = t.Name,
            Contract = t.ClientServiceContract?.Label ?? "",
            CarrierService = t.ClientCarrierService?.ServiceLabel ?? "",
            Probe = t.ProbeType.ToString(),
            Address = !string.IsNullOrWhiteSpace(t.IpAddress) ? t.IpAddress : t.Fqdn,
            IntervalSeconds = t.CheckIntervalSeconds,
            Status = t.LastStatus,
            LastCheckedAt = t.LastCheckedAt,
            LastError = t.LastError,
            IsActive = t.IsActive
        }).ToList();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var t = await _db.MonitorTargets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        t.IsActive = !t.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public class Row
    {
        public Guid Id { get; set; }
        public string Client { get; set; } = "";
        public string Name { get; set; } = "";
        public string Contract { get; set; } = "";
        public string CarrierService { get; set; } = "";
        public string Probe { get; set; } = "";
        public string Address { get; set; } = "";
        public int IntervalSeconds { get; set; }
        public MonitorStatus Status { get; set; }
        public DateTime? LastCheckedAt { get; set; }
        public string LastError { get; set; } = "";
        public bool IsActive { get; set; }
        public string BadgeClass => Status switch
        {
            MonitorStatus.Up => "text-bg-success",
            MonitorStatus.Down => "text-bg-danger",
            _ => "text-bg-secondary"
        };
    }
}
