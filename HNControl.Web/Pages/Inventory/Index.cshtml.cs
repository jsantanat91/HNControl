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

    // Search
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    // Filters (Excel-style)
    [BindProperty(SupportsGet = true)]
    public string? Cat { get; set; } // category text, "__none" = empty

    [BindProperty(SupportsGet = true)]
    public string? Loc { get; set; } // location text, "__none" = null/empty

    [BindProperty(SupportsGet = true)]
    public Guid? BrandId { get; set; } // "__none" handled in view by empty Guid

    // Sort
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; } // name|category|brand|location|stock

    [BindProperty(SupportsGet = true)]
    public string? Dir { get; set; } // asc|desc

    // Paging
    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public int PageSize { get; } = 50;

    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public int LowStockCount { get; private set; }
    public int ZeroStockCount { get; private set; }

    public int From => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);

    // Data
    public List<InventoryItem> Items { get; set; } = new();

    // Filter options
    public List<string> CategoryOptions { get; private set; } = new();
    public List<string> LocationOptions { get; private set; } = new();
    public List<InventoryBrand> BrandOptions { get; private set; } = new();

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
        // Options first (for selects)
        CategoryOptions = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive)
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        LocationOptions = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive && i.Location != null && i.Location != "")
            .Select(i => i.Location!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        BrandOptions = await _db.InventoryBrands.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var q = (Q ?? "").Trim();
        var query = _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Brand)
            .Where(i => i.IsActive)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(Cat))
        {
            if (Cat == "__none")
                query = query.Where(i => (i.Category ?? "") == "");
            else
                query = query.Where(i => i.Category == Cat);
        }

        if (!string.IsNullOrWhiteSpace(Loc))
        {
            if (Loc == "__none")
                query = query.Where(i => i.Location == null || i.Location == "");
            else
                query = query.Where(i => i.Location == Loc);
        }

        if (BrandId.HasValue && BrandId.Value != Guid.Empty)
            query = query.Where(i => i.BrandId == BrandId.Value);

        if (BrandId.HasValue && BrandId.Value == Guid.Empty)
            query = query.Where(i => i.BrandId == null);

        // Search (any field)
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
        LowStockCount = await query.CountAsync(i => i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel);
        ZeroStockCount = await query.CountAsync(i => i.QuantityOnHand <= 0);

        if (Page < 1) Page = 1;
        var totalPages = TotalPages;
        if (totalPages > 0 && Page > totalPages) Page = totalPages;

        // Sort
        var sort = (Sort ?? "name").Trim().ToLowerInvariant();
        var desc = string.Equals((Dir ?? "asc").Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<InventoryItem> ordered = sort switch
        {
            "category" => desc
                ? query.OrderByDescending(i => i.Category).ThenBy(i => i.Name)
                : query.OrderBy(i => i.Category).ThenBy(i => i.Name),

            "location" => desc
                ? query.OrderByDescending(i => i.Location ?? "").ThenBy(i => i.Name)
                : query.OrderBy(i => i.Location ?? "").ThenBy(i => i.Name),

            "brand" => desc
                ? query.OrderByDescending(i => (i.Brand != null ? (i.Brand.Name ?? "") : "")).ThenBy(i => i.Name)
                : query.OrderBy(i => (i.Brand != null ? (i.Brand.Name ?? "") : "")).ThenBy(i => i.Name),

            "stock" => desc
                ? query.OrderByDescending(i => i.QuantityOnHand).ThenBy(i => i.Name)
                : query.OrderBy(i => i.QuantityOnHand).ThenBy(i => i.Name),

            _ => desc
                ? query.OrderByDescending(i => i.Name)
                : query.OrderBy(i => i.Name)
        };

        Items = await ordered
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
