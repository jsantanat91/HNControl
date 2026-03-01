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

        // Backfill tokens si vienen de órdenes viejas.
        var changed = false;
        foreach (var o in list)
        {
            if (string.IsNullOrWhiteSpace(o.PublicToken))
            {
                o.PublicToken = Guid.NewGuid().ToString("N");
                changed = true;
            }
        }
        if (changed)
            await _db.SaveChangesAsync();

        Rows = list.Select(o =>
        {
            // ✅ Ruta pública directa para descargar PDF
            // (si no existe PDF aún, el endpoint lo genera al vuelo)
            var publicUrl = $"{Request.Scheme}://{Request.Host}/Public/ServiceOrder/{o.PublicToken}?handler=DownloadPdf";

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
