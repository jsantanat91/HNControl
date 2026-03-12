using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Movements;

[Authorize(Policy = "InventorySupervisor")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Type { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public Guid? ItemId { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;

    public List<SelectListItem> ItemOptions { get; set; } = new();
    public List<InventoryMovement> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int From { get; set; }
    public int To { get; set; }

    public async Task OnGetAsync()
    {
        await LoadItemOptionsAsync();
        if (Page < 1) Page = 1;
        const int pageSize = 20;

        var qry = _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .AsQueryable();

        if (ItemId.HasValue && ItemId.Value != Guid.Empty)
            qry = qry.Where(m => m.ItemId == ItemId.Value);

        if (!string.IsNullOrWhiteSpace(Type) && !string.Equals(Type, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(Type, "in", StringComparison.OrdinalIgnoreCase))
                qry = qry.Where(m => m.Type == InventoryMovementType.In);
            else if (string.Equals(Type, "out", StringComparison.OrdinalIgnoreCase))
                qry = qry.Where(m => m.Type == InventoryMovementType.Out);
        }

        if (!string.IsNullOrWhiteSpace(Status) && !string.Equals(Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(Status, "pending", StringComparison.OrdinalIgnoreCase))
                qry = qry.Where(m => m.Status == InventoryMovementStatus.Pending);
            else if (string.Equals(Status, "approved", StringComparison.OrdinalIgnoreCase))
                qry = qry.Where(m => m.Status == InventoryMovementStatus.Approved);
            else if (string.Equals(Status, "rejected", StringComparison.OrdinalIgnoreCase))
                qry = qry.Where(m => m.Status == InventoryMovementStatus.Rejected);
        }

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var s = Q.Trim();
            var pat = $"%{s}%";

            qry = qry.Where(m =>
                EF.Functions.ILike(m.Item!.Name, pat) ||
                EF.Functions.ILike(m.Item!.Sku ?? "", pat) ||
                EF.Functions.ILike(m.RequestedByName ?? "", pat) ||
                EF.Functions.ILike(m.ResponsibleName ?? "", pat) ||
                EF.Functions.ILike(m.SerialNumber ?? "", pat) ||
                EF.Functions.ILike(m.Reference ?? "", pat) ||
                EF.Functions.ILike(m.Notes ?? "", pat));
        }

        TotalCount = await qry.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)pageSize));
        if (Page > TotalPages) Page = TotalPages;

        Rows = await qry
            .OrderByDescending(m => m.RequestedAt)
            .Skip((Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (TotalCount == 0)
        {
            From = 0;
            To = 0;
        }
        else
        {
            From = ((Page - 1) * pageSize) + 1;
            To = Math.Min(Page * pageSize, TotalCount);
        }
    }

    private async Task LoadItemOptionsAsync()
    {
        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Take(400)
            .Select(i => new { i.Id, i.Name, i.Sku })
            .ToListAsync();

        ItemOptions = new List<SelectListItem>
        {
            new SelectListItem("(Todos)", "")
        };

        ItemOptions.AddRange(items.Select(i => new SelectListItem(
            string.IsNullOrWhiteSpace(i.Sku) ? i.Name : $"{i.Name} [{i.Sku}]",
            i.Id.ToString())));
    }
}

