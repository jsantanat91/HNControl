using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(Guid Id, string Name, string Kind, string Email, string Services);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var clients = await _db.Clients
            .Include(c => c.Services)
            .OrderBy(c => c.Name)
            .ToListAsync();

        Rows = clients.Select(c =>
            new Row(
                c.Id,
                c.Name,
                c.Kind.ToString(),                // UI friendly (alias)
                c.Email ?? "",                    // ✅ evita CS8604
                string.Join(", ",
                    (c.Services ?? new List<ClientService>())
                        .Select(s => s.ServiceType.ToString())
                )
            )
        ).ToList();
    }
}
