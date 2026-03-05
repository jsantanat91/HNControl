using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Carriers.Services;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public Guid? ClientId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<Client> Clients { get; set; } = new();
    public List<ClientCarrierService> Services { get; set; } = new();

    public async Task OnGetAsync()
    {
        Clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        var query = _db.ClientCarrierServices
            .AsNoTracking()
            .Include(s => s.Client)
            .Include(s => s.Carrier)
            .Include(s => s.ClientServiceContract)
            .OrderBy(s => s.Client!.Name)
            .ThenBy(s => s.ServiceLabel)
            .AsQueryable();

        if (ClientId.HasValue)
            query = query.Where(s => s.ClientId == ClientId.Value);

        var q = (Q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(s =>
                (s.ServiceLabel ?? "").ToLower().Contains(q.ToLower()) ||
                (s.AccountNumber ?? "").ToLower().Contains(q.ToLower()) ||
                (s.ContractNumber ?? "").ToLower().Contains(q.ToLower()) ||
                (s.CircuitId ?? "").ToLower().Contains(q.ToLower()) ||
                (s.ClientServiceContract != null && (s.ClientServiceContract.Label ?? "").ToLower().Contains(q.ToLower())) ||
                (s.Client!.Name ?? "").ToLower().Contains(q.ToLower()) ||
                (s.Carrier!.Name ?? "").ToLower().Contains(q.ToLower()));
        }

        Services = await query.Take(500).ToListAsync();
    }
}
