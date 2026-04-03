using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;
    public IndexModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true)] public string View { get; set; } = "normal";
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanEdit { get; set; }

    public record Row(Guid Id, string ClientCode, string Name, string Rfc, string Kind, string Email, string ContractsSummary, DateTime CreatedAt, bool IsActive, bool IsTemporaryLead);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        CanEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        await EnsureClientCodesAsync();

        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var today = DateTime.Today;
        var soon = today.AddDays(30);

        var q = _db.Clients
            .Include(c => c.Contracts)
            .AsQueryable();

        var showLeads = string.Equals(View, "leads", StringComparison.OrdinalIgnoreCase);
        q = q.Where(c => c.IsTemporaryLead == showLeads);

        var name = (Name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(c => c.Name.ToLower().Contains(name.ToLower()));

        TotalCount = await q.CountAsync();

        var clients = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
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
                ? "-"
                : $"{total} contrato(s)" + (expSoon > 0 ? $" · {expSoon} por vencer" : "") + (top.Any() ? $" · {string.Join(", ", top)}" : "");

            return new Row(
                c.Id,
                c.ClientCode,
                c.Name,
                c.Rfc ?? "",
                c.Kind.ToString(),
                c.Email ?? "",
                summary,
                c.CreatedAt,
                c.IsActive,
                c.IsTemporaryLead
            );
        }).ToList();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(Guid id, string? name, string? view, int page = 1, int pageSize = 20)
    {
        var canEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        if (!canEdit) return Forbid();

        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (client == null) return RedirectToPage(new { Name = name, View = view, Page = page, PageSize = pageSize });
        client.IsActive = !client.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { Name = name, View = view, Page = page, PageSize = pageSize });
    }

    private async Task EnsureClientCodesAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.CreatedAt).ToListAsync();
        var used = new HashSet<int>();
        var max = 0;

        foreach (var c in clients)
        {
            if (!string.IsNullOrWhiteSpace(c.ClientCode) &&
                c.ClientCode.StartsWith("HN-") &&
                !c.ClientCode.StartsWith("HN-VENTA-") &&
                int.TryParse(c.ClientCode.AsSpan(3), out var n))
            {
                used.Add(n);
                if (n > max) max = n;
            }
        }

        var changed = false;
        foreach (var c in clients.Where(c => string.IsNullOrWhiteSpace(c.ClientCode) && !c.IsTemporaryLead))
        {
            do { max++; } while (used.Contains(max));
            c.ClientCode = $"HN-{max:0000}";
            used.Add(max);
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync();
    }
}


