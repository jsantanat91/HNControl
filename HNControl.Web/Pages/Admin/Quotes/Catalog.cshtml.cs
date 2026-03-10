using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Quotes;

public class CatalogModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CatalogModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public CatalogInput Input { get; set; } = new();
    [BindProperty]
    public RuleInput Rule { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public List<CatalogRowVm> Items { get; set; } = [];
    public List<ParentOptionVm> ParentOptions { get; set; } = [];
    public List<RuleRowVm> Rules { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError(string.Empty, "Nombre requerido.");
            await LoadAsync();
            return Page();
        }

        if (!ValidateParent(Input.NodeType, Input.ParentId, Input.Segment, out var parentError))
        {
            ModelState.AddModelError(string.Empty, parentError);
            await LoadAsync();
            return Page();
        }

        var item = new QuoteCatalogItem
        {
            Segment = Input.Segment,
            NodeType = Input.NodeType,
            ParentId = Input.ParentId,
            Name = Input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            UnitPrice = Input.UnitPrice,
            UnitPriceIncludesVat = Input.UnitPriceIncludesVat,
            IsManualPrice = Input.IsManualPrice,
            ReferenceUrl = string.IsNullOrWhiteSpace(Input.ReferenceUrl) ? null : Input.ReferenceUrl.Trim(),
            SortOrder = Input.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.QuoteCatalogItems.Add(item);
        await _db.SaveChangesAsync();

        Message = "Item guardado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var item = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return RedirectToPage();

        item.IsActive = !item.IsActive;
        await _db.SaveChangesAsync();

        Message = item.IsActive ? "Item activado." : "Item desactivado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateRuleAsync()
    {
        if (Rule.TargetItemId == Guid.Empty || Rule.RequiredItemId == Guid.Empty)
        {
            Message = "Selecciona item objetivo y item requerido.";
            return RedirectToPage();
        }
        if (Rule.TargetItemId == Rule.RequiredItemId)
        {
            Message = "El item objetivo y requerido no pueden ser el mismo.";
            return RedirectToPage();
        }

        var target = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == Rule.TargetItemId);
        var required = await _db.QuoteCatalogItems.FirstOrDefaultAsync(x => x.Id == Rule.RequiredItemId);
        if (target == null || required == null)
        {
            Message = "No se encontro el item de la regla.";
            return RedirectToPage();
        }
        if (target.Segment != required.Segment)
        {
            Message = "La regla debe estar en el mismo segmento.";
            return RedirectToPage();
        }

        var exists = await _db.QuoteCatalogRules.AnyAsync(x =>
            x.TargetItemId == Rule.TargetItemId &&
            x.RequiredItemId == Rule.RequiredItemId &&
            x.Segment == target.Segment);
        if (exists)
        {
            Message = "La regla ya existe.";
            return RedirectToPage();
        }

        _db.QuoteCatalogRules.Add(new QuoteCatalogRule
        {
            Segment = target.Segment,
            TargetItemId = target.Id,
            RequiredItemId = required.Id,
            Action = QuoteRuleAction.ShowOnlyIfSelected,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        Message = "Regla guardada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteRuleAsync(Guid id)
    {
        var rule = await _db.QuoteCatalogRules.FirstOrDefaultAsync(x => x.Id == id);
        if (rule == null) return RedirectToPage();
        _db.QuoteCatalogRules.Remove(rule);
        await _db.SaveChangesAsync();
        Message = "Regla eliminada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSeedDemoAsync()
    {
        if (await _db.QuoteCatalogItems.AnyAsync())
        {
            Message = "Ya existe catalogo. No se cargo demo.";
            return RedirectToPage();
        }

        var resCat = new QuoteCatalogItem
        {
            Segment = QuoteSegment.Residential,
            NodeType = QuoteNodeType.Category,
            Name = "Sistema CCTV",
            SortOrder = 10,
            IsActive = true
        };
        var bizCat = new QuoteCatalogItem
        {
            Segment = QuoteSegment.Business,
            NodeType = QuoteNodeType.Category,
            Name = "Servicios de servidor",
            SortOrder = 10,
            IsActive = true
        };

        _db.QuoteCatalogItems.AddRange(resCat, bizCat);
        await _db.SaveChangesAsync();

        var resService = new QuoteCatalogItem
        {
            Segment = QuoteSegment.Residential,
            NodeType = QuoteNodeType.Service,
            ParentId = resCat.Id,
            Name = "Tecnologia de camaras",
            Description = "Selecciona PIR o ColorVu para comparar.",
            ReferenceUrl = "https://www.hikvision.com",
            SortOrder = 10,
            IsActive = true
        };

        var bizService = new QuoteCatalogItem
        {
            Segment = QuoteSegment.Business,
            NodeType = QuoteNodeType.Service,
            ParentId = bizCat.Id,
            Name = "Servidor",
            Description = "Cloud o fisico.",
            SortOrder = 10,
            IsActive = true
        };

        _db.QuoteCatalogItems.AddRange(resService, bizService);
        await _db.SaveChangesAsync();

        _db.QuoteCatalogItems.AddRange(
            new QuoteCatalogItem
            {
                Segment = QuoteSegment.Residential,
                NodeType = QuoteNodeType.Subproduct,
                ParentId = resService.Id,
                Name = "CCTV 2 camaras ColorVu",
                UnitPrice = 9500m,
                SortOrder = 10,
                IsActive = true
            },
            new QuoteCatalogItem
            {
                Segment = QuoteSegment.Residential,
                NodeType = QuoteNodeType.Subproduct,
                ParentId = resService.Id,
                Name = "CCTV 4 camaras PIR",
                UnitPrice = 13800m,
                SortOrder = 20,
                IsActive = true
            },
            new QuoteCatalogItem
            {
                Segment = QuoteSegment.Business,
                NodeType = QuoteNodeType.Subproduct,
                ParentId = bizService.Id,
                Name = "Cloud AWS",
                IsManualPrice = true,
                SortOrder = 10,
                IsActive = true
            },
            new QuoteCatalogItem
            {
                Segment = QuoteSegment.Business,
                NodeType = QuoteNodeType.Subproduct,
                ParentId = bizService.Id,
                Name = "Fisico Dell",
                IsManualPrice = true,
                SortOrder = 20,
                IsActive = true
            }
        );

        await _db.SaveChangesAsync();
        Message = "Demo cargado.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var raw = await _db.QuoteCatalogItems
            .AsNoTracking()
            .OrderBy(x => x.Segment)
            .ThenBy(x => x.NodeType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var byId = raw.ToDictionary(x => x.Id, x => x.Name);

        Items = raw.Select(x => new CatalogRowVm
        {
            Id = x.Id,
            Segment = x.Segment,
            NodeType = x.NodeType,
            Name = x.Name,
            Description = x.Description,
            UnitPrice = x.UnitPrice,
            UnitPriceIncludesVat = x.UnitPriceIncludesVat,
            IsManualPrice = x.IsManualPrice,
            ReferenceUrl = x.ReferenceUrl,
            IsActive = x.IsActive,
            ParentName = x.ParentId.HasValue && byId.TryGetValue(x.ParentId.Value, out var p) ? p : null
        }).ToList();

        ParentOptions = raw.Select(x => new ParentOptionVm
        {
            Id = x.Id,
            Segment = x.Segment,
            NodeType = x.NodeType,
            Label = $"{LabelSegment(x.Segment)} · {LabelType(x.NodeType)} · {x.Name}"
        }).ToList();

        var rules = await _db.QuoteCatalogRules
            .AsNoTracking()
            .OrderBy(x => x.Segment)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

        Rules = rules.Select(x => new RuleRowVm
        {
            Id = x.Id,
            Segment = x.Segment,
            TargetName = byId.TryGetValue(x.TargetItemId, out var target) ? target : "(item)",
            RequiredName = byId.TryGetValue(x.RequiredItemId, out var req) ? req : "(item)",
            IsActive = x.IsActive
        }).ToList();
    }

    private bool ValidateParent(QuoteNodeType nodeType, Guid? parentId, QuoteSegment segment, out string error)
    {
        error = string.Empty;
        if (nodeType == QuoteNodeType.Category)
        {
            if (parentId.HasValue)
            {
                error = "Categoria no debe tener padre.";
                return false;
            }
            return true;
        }

        if (!parentId.HasValue)
        {
            error = "Este nivel requiere un padre.";
            return false;
        }

        var parent = _db.QuoteCatalogItems.FirstOrDefault(x => x.Id == parentId.Value);
        if (parent == null)
        {
            error = "Padre no encontrado.";
            return false;
        }

        if (parent.Segment != segment)
        {
            error = "El padre debe ser del mismo segmento.";
            return false;
        }

        if (nodeType == QuoteNodeType.Service && parent.NodeType != QuoteNodeType.Category)
        {
            error = "Un servicio debe depender de una categoria.";
            return false;
        }

        if (nodeType == QuoteNodeType.Subproduct && parent.NodeType != QuoteNodeType.Service)
        {
            error = "Un subproducto debe depender de un servicio.";
            return false;
        }

        return true;
    }

    public string LabelSegment(QuoteSegment x) => x == QuoteSegment.Business ? "Empresarial" : "Residencial";

    public string LabelType(QuoteNodeType x) => x switch
    {
        QuoteNodeType.Category => "Categoria",
        QuoteNodeType.Service => "Servicio",
        QuoteNodeType.Subproduct => "Subproducto",
        _ => x.ToString()
    };

    public class CatalogInput
    {
        public QuoteSegment Segment { get; set; } = QuoteSegment.Residential;
        public QuoteNodeType NodeType { get; set; } = QuoteNodeType.Category;
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool UnitPriceIncludesVat { get; set; }
        public bool IsManualPrice { get; set; }
        public string? ReferenceUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public class CatalogRowVm
    {
        public Guid Id { get; set; }
        public QuoteSegment Segment { get; set; }
        public QuoteNodeType NodeType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool UnitPriceIncludesVat { get; set; }
        public bool IsManualPrice { get; set; }
        public string? ReferenceUrl { get; set; }
        public bool IsActive { get; set; }
        public string? ParentName { get; set; }
    }

    public class ParentOptionVm
    {
        public Guid Id { get; set; }
        public QuoteSegment Segment { get; set; }
        public QuoteNodeType NodeType { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class RuleInput
    {
        public Guid TargetItemId { get; set; }
        public Guid RequiredItemId { get; set; }
    }

    public class RuleRowVm
    {
        public Guid Id { get; set; }
        public QuoteSegment Segment { get; set; }
        public string TargetName { get; set; } = string.Empty;
        public string RequiredName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
