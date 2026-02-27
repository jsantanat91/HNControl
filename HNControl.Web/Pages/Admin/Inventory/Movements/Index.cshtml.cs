using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Movements;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Type { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public Guid? ItemId { get; set; }

    public List<SelectListItem> ItemOptions { get; set; } = new();
    public List<InventoryMovement> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadItemOptionsAsync();

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

        Rows = await qry
            .OrderByDescending(m => m.RequestedAt)
            .Take(1000)
            .ToListAsync();
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
