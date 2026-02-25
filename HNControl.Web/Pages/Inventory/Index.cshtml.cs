using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public int PageSize { get; } = 50;

    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int From => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);

    public List<InventoryItem> Items { get; set; } = new();

    private static readonly string[] _badgePalette = new[]
    {
        "hn-badge-blue",
        "hn-badge-green",
        "hn-badge-amber",
        "hn-badge-purple",
        "hn-badge-cyan",
        "hn-badge-slate"
    };

    public string GetCategoryBadgeClass(string? category)
    {
        var key = (category ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key))
            return "hn-badge-slate";

        var h = StableHash(key.ToLowerInvariant());
        var ix = Math.Abs(h) % _badgePalette.Length;
        return _badgePalette[ix];
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int hash = 23;
            foreach (var ch in s)
                hash = (hash * 31) + ch;
            return hash;
        }
    }

    public async Task OnGetAsync()
    {
        var q = (Q ?? "").Trim();
        var query = _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Brand)
            .Where(i => i.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var l = q.ToLowerInvariant();
            query = query.Where(i =>
                (i.Name ?? "").ToLower().Contains(l) ||
                (i.Sku ?? "").ToLower().Contains(l) ||
                (i.Category ?? "").ToLower().Contains(l) ||
                (i.Location ?? "").ToLower().Contains(l) ||
                (i.Model ?? "").ToLower().Contains(l) ||
                (i.Unit ?? "").ToLower().Contains(l) ||
                (i.Notes ?? "").ToLower().Contains(l) ||
                (i.Brand != null && (i.Brand.Name ?? "").ToLower().Contains(l))
            );
        }

        TotalCount = await query.CountAsync();

        if (Page < 1) Page = 1;
        var totalPages = TotalPages;
        if (totalPages > 0 && Page > totalPages) Page = totalPages;

        Items = await query
            .OrderBy(i => i.Name)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
