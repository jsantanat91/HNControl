using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;

    public IndexModel(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public record Row(Guid Id, string ClientName, string Title, string Type, string Status, string Assigned, string PublicUrl);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var baseUrl = GetPublicBaseUrl();

        var list = await _db.ServiceOrders
            .Include(o => o.Client)
            .Include(o => o.AssignedEmployee)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync();

        Rows = list.Select(o => new Row(
            o.Id,
            o.Client?.Name ?? "",
            o.Title,
            o.Type.ToString(),
            o.Status.ToString(),
            o.AssignedEmployee?.FullName ?? o.AssignedUserId,
            $"{baseUrl}/Public/ServiceOrder/{o.PublicToken}"
        )).ToList();
    }

    private string GetPublicBaseUrl()
    {
        var cfgBase = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(cfgBase))
            return cfgBase.TrimEnd('/');

        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        return $"{scheme}://{host}".TrimEnd('/');
    }
}
