using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Client? Client { get; set; }
    public List<string> Services { get; set; } = new();

    public record ProjectRow(Guid Id, string Title, string Responsible, string StartDate, string EstEnd, string Status);
    public List<ProjectRow> Projects { get; set; } = new();

    public async Task OnGetAsync(Guid id)
    {
        Client = await _db.Clients
            .Include(c => c.Services)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (Client == null) return;

        Services = Client.Services.Select(s => s.ServiceType.ToString()).ToList();

        var projs = await _db.Projects
            .Include(p => p.AssignedEmployee)
            .Where(p => p.ClientId == id)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        Projects = projs.Select(p => new ProjectRow(
            p.Id,
            p.Title,
            p.AssignedEmployee?.FullName ?? p.AssignedUserId,
            p.StartDate.ToString("yyyy-MM-dd"),
            p.EstimatedEndDate.ToString("yyyy-MM-dd"),
            p.Status.ToString()
        )).ToList();
    }
}
