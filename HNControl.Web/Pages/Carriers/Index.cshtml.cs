using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Carriers;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<ClientCard> Clients { get; set; } = new();

    public class ClientCard
    {
        public Guid ClientId { get; set; }
        public string Name { get; set; } = "";
        public int ServicesCount { get; set; }
        public string CarriersSummary { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        var q = (Q ?? "").Trim();

        var clients = await _db.Clients
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            clients = clients
                .Where(c => (c.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                         || (c.Rfc ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var clientIds = clients.Select(c => c.Id).ToList();

        var services = await _db.ClientCarrierServices
            .AsNoTracking()
            .Include(s => s.Carrier)
            .Where(s => clientIds.Contains(s.ClientId) && s.IsActive)
            .ToListAsync();

        var grouped = services
            .GroupBy(s => s.ClientId)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                Carriers = string.Join(", ", g
                    .Select(x => x.Carrier != null ? x.Carrier.Name : "(Sin carrier)")
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3))
            });

        Clients = clients.Select(c =>
        {
            grouped.TryGetValue(c.Id, out var g);
            return new ClientCard
            {
                ClientId = c.Id,
                Name = c.Name,
                ServicesCount = g?.Count ?? 0,
                CarriersSummary = g?.Carriers ?? ""
            };
        }).ToList();
    }
}
