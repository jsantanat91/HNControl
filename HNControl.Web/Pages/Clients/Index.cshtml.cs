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

    public record Row(Guid Id, string Name, string Rfc, string Kind, string Email, string ContractsSummary);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var today = DateTime.Today;
        var soon = today.AddDays(30);

        var clients = await _db.Clients
            .Include(c => c.Contracts)
            .OrderBy(c => c.Name)
            .ToListAsync();

        Rows = clients.Select(c =>
        {
            var total = c.Contracts.Count;
            var expSoon = c.Contracts.Count(x => x.ContractEndDate.HasValue && x.ContractEndDate.Value.Date <= soon);

            var top = c.Contracts
                .GroupBy(x => x.ServiceType)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key.ToString())
                .ToList();

            var summary = total == 0
                ? "—"
                : $"{total} contrato(s)" + (expSoon > 0 ? $" · {expSoon} por vencer" : "") + (top.Any() ? $" · {string.Join(", ", top)}" : "");

            return new Row(
                c.Id,
                c.Name,
                c.Rfc ?? "",
                c.Kind.ToString(),
                c.Email ?? "",
                summary
            );
        }).ToList();
    }
}
