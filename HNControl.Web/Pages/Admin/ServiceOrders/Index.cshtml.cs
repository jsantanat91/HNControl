using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(
        Guid Id,
        string Title,
        string ClientName,
        string ProjectTitle,
        string ContractLabel,
        string Type,
        string Status,
        string Assigned,
        string PublicUrl,
        string CreatedAt
    );

    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var list = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.Project)
            .Include(o => o.ClientServiceContract)
            .Include(o => o.AssignedEmployee)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync();

        Rows = list.Select(o =>
        {
            // Ruta pública: /Public/ServiceOrder/{token}
            var publicUrl = $"{Request.Scheme}://{Request.Host}/Public/ServiceOrder/{o.PublicToken}";

            return new Row(
                o.Id,
                o.Title,
                o.Client?.Name ?? "-",
                o.Project?.Title ?? "-",
                o.ClientServiceContract?.Label ?? "-",
                o.Type.GetDisplayName(),
                o.Status.GetDisplayName(),
                o.AssignedEmployee?.FullName ?? "-",
                publicUrl,
                o.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")
            );
        }).ToList();
    }
}
