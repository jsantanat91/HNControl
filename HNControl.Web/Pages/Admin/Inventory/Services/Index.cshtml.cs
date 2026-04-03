using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Services;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private const string MarkerCategory = "INV_SERVICE_PACKAGE_CATEGORY";
    private const string MarkerPackage = "INV_SERVICE_PACKAGE";

    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [TempData] public string? Info { get; set; }
    [TempData] public string? Error { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public List<RowVm> Rows { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(140)]
        public string Name { get; set; } = "";

        [MaxLength(1200)]
        public string Description { get; set; } = "";

        public ClientServiceType ServiceType { get; set; } = ClientServiceType.Otro;
        public QuoteOfferType OfferType { get; set; } = QuoteOfferType.MonthlyRent;

        [Range(0, 99999999)]
        public decimal UnitPrice { get; set; } = 0m;
    }

    public record RowVm(
        Guid Id,
        string Name,
        string Description,
        ClientServiceType ServiceType,
        QuoteOfferType OfferType,
        decimal? UnitPrice,
        bool IsActive,
        int SortOrder);

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var categoryId = await EnsureCategoryAsync();
        var nextSort = await _db.QuoteCatalogItems
            .Where(x => x.NodeType == QuoteNodeType.Service && x.VariantGroup == MarkerPackage)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? 0;

        var item = new QuoteCatalogItem
        {
            Segment = QuoteSegment.Business,
            NodeType = QuoteNodeType.Service,
            ParentId = categoryId,
            Name = (Input.Name ?? "").Trim(),
            Description = (Input.Description ?? "").Trim(),
            UnitPrice = Input.UnitPrice <= 0 ? null : Math.Round(Input.UnitPrice, 2),
            UnitPriceIncludesVat = false,
            IsManualPrice = Input.UnitPrice <= 0,
            OfferType = Input.OfferType,
            VariantGroup = MarkerPackage,
            VariantValue = Input.ServiceType.ToString(),
            SortOrder = nextSort + 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.QuoteCatalogItems.Add(item);
        await _db.SaveChangesAsync();
        Info = "Paquete de servicio agregado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var item = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == id && x.VariantGroup == MarkerPackage);
        if (item == null) return RedirectToPage();

        item.IsActive = !item.IsActive;
        await _db.SaveChangesAsync();
        Info = item.IsActive ? "Paquete activado." : "Paquete desactivado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, string name, string description, string serviceType, string offerType, decimal? unitPrice)
    {
        var item = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == id && x.VariantGroup == MarkerPackage);
        if (item == null) return RedirectToPage();

        item.Name = (name ?? "").Trim();
        item.Description = (description ?? "").Trim();

        if (Enum.TryParse<ClientServiceType>(serviceType, true, out var parsedType))
            item.VariantValue = parsedType.ToString();

        if (Enum.TryParse<QuoteOfferType>(offerType, true, out var parsedOffer))
            item.OfferType = parsedOffer;

        var price = (unitPrice ?? 0m);
        item.UnitPrice = price <= 0 ? null : Math.Round(price, 2);
        item.IsManualPrice = price <= 0;
        item.UnitPriceIncludesVat = false;

        await _db.SaveChangesAsync();
        Info = "Paquete actualizado.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var data = await _db.QuoteCatalogItems
            .AsNoTracking()
            .Where(x => x.NodeType == QuoteNodeType.Service && x.VariantGroup == MarkerPackage)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        Rows = data.Select(x => new RowVm(
            x.Id,
            x.Name,
            x.Description ?? "",
            Enum.TryParse<ClientServiceType>(x.VariantValue, true, out var t) ? t : ClientServiceType.Otro,
            x.OfferType,
            x.UnitPrice,
            x.IsActive,
            x.SortOrder
        )).ToList();
    }

    private async Task<Guid> EnsureCategoryAsync()
    {
        var category = await _db.QuoteCatalogItems
            .FirstOrDefaultAsync(x => x.NodeType == QuoteNodeType.Category && x.VariantGroup == MarkerCategory);
        if (category != null) return category.Id;

        category = new QuoteCatalogItem
        {
            Segment = QuoteSegment.Business,
            NodeType = QuoteNodeType.Category,
            ParentId = null,
            Name = "Servicios",
            Description = "Paquetes de servicios para contratos",
            UnitPrice = null,
            UnitPriceIncludesVat = false,
            IsManualPrice = true,
            OfferType = QuoteOfferType.MonthlyRent,
            VariantGroup = MarkerCategory,
            VariantValue = null,
            SortOrder = 9000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.QuoteCatalogItems.Add(category);
        await _db.SaveChangesAsync();
        return category.Id;
    }
}
