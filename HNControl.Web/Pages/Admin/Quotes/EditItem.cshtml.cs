using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Quotes;

public class EditItemModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public EditItemModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public EditInput Input { get; set; } = new();

    public bool ItemNotFound { get; set; }
    [TempData] public string? Message { get; set; }
    public List<ParentOptionVm> ParentOptions { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        var item = await _db.QuoteCatalogItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null)
        {
            ItemNotFound = true;
            return;
        }

        Input = new EditInput
        {
            Id = item.Id,
            Segment = item.Segment,
            NodeType = item.NodeType,
            ParentId = item.ParentId,
            Name = item.Name,
            Description = item.Description,
            ImageUrl = item.ImageUrl,
            UnitPrice = item.UnitPrice,
            UnitPriceIncludesVat = item.UnitPriceIncludesVat,
            IsManualPrice = item.IsManualPrice,
            OfferType = item.OfferType,
            VariantGroup = item.VariantGroup,
            VariantValue = item.VariantValue,
            ReferenceUrl = item.ReferenceUrl,
            SortOrder = item.SortOrder
        };

        await LoadParentsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var item = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (item == null)
        {
            ItemNotFound = true;
            await LoadParentsAsync();
            return Page();
        }

        item.Segment = Input.Segment;
        item.NodeType = Input.NodeType;
        item.ParentId = Input.ParentId;
        item.Name = (Input.Name ?? string.Empty).Trim();
        item.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        item.ImageUrl = string.IsNullOrWhiteSpace(Input.ImageUrl) ? null : Input.ImageUrl.Trim();
        item.UnitPrice = Input.UnitPrice;
        item.UnitPriceIncludesVat = Input.UnitPriceIncludesVat;
        item.IsManualPrice = Input.IsManualPrice;
        item.OfferType = Input.OfferType;
        item.VariantGroup = string.IsNullOrWhiteSpace(Input.VariantGroup) ? null : Input.VariantGroup.Trim();
        item.VariantValue = string.IsNullOrWhiteSpace(Input.VariantValue) ? null : Input.VariantValue.Trim();
        item.ReferenceUrl = string.IsNullOrWhiteSpace(Input.ReferenceUrl) ? null : Input.ReferenceUrl.Trim();
        item.SortOrder = Input.SortOrder;

        await _db.SaveChangesAsync();
        Message = "Item actualizado.";
        return RedirectToPage("./Catalog");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var item = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (item == null) return RedirectToPage("./Catalog");

        var hasChildren = await _db.QuoteCatalogItems.AnyAsync(x => x.ParentId == item.Id);
        if (hasChildren)
        {
            Message = "No se puede eliminar: primero elimina sus hijos.";
            return RedirectToPage("./EditItem", new { id = item.Id });
        }

        var rules = await _db.QuoteCatalogRules.Where(x => x.TargetItemId == item.Id || x.RequiredItemId == item.Id).ToListAsync();
        if (rules.Count > 0)
            _db.QuoteCatalogRules.RemoveRange(rules);

        _db.QuoteCatalogItems.Remove(item);
        await _db.SaveChangesAsync();

        Message = "Item eliminado.";
        return RedirectToPage("./Catalog");
    }

    private async Task LoadParentsAsync()
    {
        var raw = await _db.QuoteCatalogItems
            .AsNoTracking()
            .OrderBy(x => x.Segment)
            .ThenBy(x => x.NodeType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        ParentOptions = raw
            .Where(x => x.Id != Input.Id)
            .Select(x => new ParentOptionVm
            {
                Id = x.Id,
                Label = $"{(x.Segment == QuoteSegment.Business ? "Empresarial" : "Residencial")} · {x.NodeType} · {x.Name}"
            }).ToList();
    }

    public class EditInput
    {
        public Guid Id { get; set; }
        public QuoteSegment Segment { get; set; }
        public QuoteNodeType NodeType { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool UnitPriceIncludesVat { get; set; }
        public bool IsManualPrice { get; set; }
        public QuoteOfferType OfferType { get; set; } = QuoteOfferType.Sale;
        public string? VariantGroup { get; set; }
        public string? VariantValue { get; set; }
        public string? ReferenceUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public class ParentOptionVm
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
