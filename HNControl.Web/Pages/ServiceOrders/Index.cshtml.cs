using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.ServiceOrders;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;

    public IndexModel(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public string? Info { get; set; }

    public record Row(Guid Id, string Client, string Title, string Type, string Status, string Created, string Due, string PublicUrl);
    public List<Row> Rows { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // Admin ya tiene su módulo
        if (User.IsInRole(AppRoles.Admin))
            return Redirect("/Admin/ServiceOrders/Index");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        baseUrl = baseUrl.TrimEnd('/');

        var orders = await _db.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Client)
            .Where(o => o.AssignedUserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        Rows = orders.Select(o => new Row(
            o.Id,
            o.Client?.Name ?? "—",
            o.Title,
            o.Type.GetDisplayName(),
            o.Status.GetDisplayName(),
            o.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            o.EstimatedEndDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "—",
            $"{baseUrl}/Public/ServiceOrder/{o.PublicToken}"
        )).ToList();

        return Page();
    }
}
