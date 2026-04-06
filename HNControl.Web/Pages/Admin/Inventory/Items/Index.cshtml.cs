using System;
using System.Globalization;
using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Items;

[Authorize(Policy = "InventorySupervisor")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<InventoryItem> Items { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    // Filters (Excel-style)
    [BindProperty(SupportsGet = true)]
    public string? Cat { get; set; } // category text, "__none" = empty

    [BindProperty(SupportsGet = true)]
    public string? Loc { get; set; } // location text, "__none" = null/empty

    [BindProperty(SupportsGet = true)]
    public Guid? BrandId { get; set; } // Guid.Empty = null (sin marca)

    // Sort
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; } // name|category|brand|location|stock|active

    [BindProperty(SupportsGet = true)]
    public string? Dir { get; set; } // asc|desc

    // Filter options
    public List<string> CategoryOptions { get; private set; } = new();
    public List<string> LocationOptions { get; private set; } = new();
    public List<InventoryBrand> BrandOptions { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public int PageSize { get; } = 50;

    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int From => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);

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

    [TempData] public string? Info { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        // Options for filters
        CategoryOptions = await _db.InventoryItems.AsNoTracking()
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        LocationOptions = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.Location != null && i.Location != "")
            .Select(i => i.Location!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        BrandOptions = await _db.InventoryBrands.AsNoTracking()
            .OrderBy(b => b.Name)
            .ToListAsync();

        var q = (Q ?? "").Trim();

        var query = _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Brand)
            .Where(i => i.IsActive)
            .AsQueryable();

        // Filters
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

        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            var l = q.ToLowerInvariant();
            query = query.Where(i =>
                (i.Name ?? "").ToLower().Contains(l) ||
                (i.ModelCode ?? "").ToLower().Contains(l) ||
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

        var sort = (Sort ?? "active").Trim().ToLowerInvariant();
        var desc = string.Equals((Dir ?? "desc").Trim(), "desc", StringComparison.OrdinalIgnoreCase);

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

            "name" => desc
                ? query.OrderByDescending(i => i.Name)
                : query.OrderBy(i => i.Name),

            _ => desc
                ? query.OrderByDescending(i => i.IsActive).ThenBy(i => i.Name)
                : query.OrderBy(i => i.IsActive).ThenBy(i => i.Name),
        };

        Items = await ordered
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

// =========================
    // TEMPLATE (Excel)
    // =========================
    public IActionResult OnGetTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Items");

        // Headers
        var headers = new[]
        {
            "Nombre",
            "ID modelo único (opcional)",
            "SKU (opcional)",
            "Categoría",
            "Marca",
            "Modelo",
            "Ubicación",
            "Unidad",
            "Existencia",
            "Stock mínimo",
            "Activo (TRUE/FALSE)",
            "Notas"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        // Example row
        ws.Cell(2, 1).Value = "Router AC";
        ws.Cell(2, 2).Value = "MDL-000001";
        ws.Cell(2, 3).Value = ""; // SKU opcional
        ws.Cell(2, 4).Value = "Routers";
        ws.Cell(2, 5).Value = "Ubiquiti";
        ws.Cell(2, 6).Value = "ER-X";
        ws.Cell(2, 7).Value = "Almacén Matamoros";
        ws.Cell(2, 8).Value = "pza";
        ws.Cell(2, 9).Value = 5;
        ws.Cell(2, 10).Value = 2;
        ws.Cell(2, 11).Value = "TRUE";
        ws.Cell(2, 12).Value = "Equipo demo";

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = "plantilla_items_inventario.xlsx";
        const string mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(bytes, mime, fileName);
    }

    // =========================
    // IMPORT (Excel)
    // =========================
    public async Task<IActionResult> OnPostImportAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            Error = "Selecciona un archivo .xlsx.";
            return RedirectToPage();
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            Error = "Formato no permitido. Usa .xlsx";
            return RedirectToPage();
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            Error = "El archivo es muy grande (máximo 10MB).";
            return RedirectToPage();
        }

        // Caches
        var brandList = await _db.InventoryBrands.ToListAsync();
        var brandByName = brandList
            .Where(b => !string.IsNullOrWhiteSpace(b.Name))
            .GroupBy(b => b.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var catList = await _db.InventoryCategories.ToListAsync();
        var catByName = catList
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var locList = await _db.InventoryLocations.ToListAsync();
        var locByName = locList
            .Where(l => !string.IsNullOrWhiteSpace(l.Name))
            .GroupBy(l => l.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        // Items existentes para match
        var items = await _db.InventoryItems.ToListAsync();
        var itemBySku = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Sku))
            .GroupBy(i => i.Sku!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var itemByName = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .GroupBy(i => i.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        int created = 0;
        int updated = 0;
        int skipped = 0;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
        {
            Error = "El Excel no tiene hojas.";
            return RedirectToPage();
        }

        // Header map
        var headerRow = ws.Row(1);
        var headerMap = BuildHeaderMap(headerRow);

        // Required columns (mínimo)
        if (!headerMap.ContainsKey("name"))
        {
            Error = "Falta la columna 'Nombre'.";
            return RedirectToPage();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow < 2)
        {
            Error = "El Excel no tiene filas de datos.";
            return RedirectToPage();
        }

        for (int r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);

            var name = GetString(row, headerMap, "name")?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                continue;
            }

            var sku = GetString(row, headerMap, "sku")?.Trim();
            var modelCode = GetString(row, headerMap, "modelcode")?.Trim();
            if (string.IsNullOrWhiteSpace(sku)) sku = null;
            if (string.IsNullOrWhiteSpace(modelCode)) modelCode = null;
            else modelCode = modelCode.ToUpperInvariant();

            var category = GetString(row, headerMap, "category")?.Trim() ?? "";
            var brandName = GetString(row, headerMap, "brand")?.Trim();
            var model = GetString(row, headerMap, "model")?.Trim();
            var location = GetString(row, headerMap, "location")?.Trim();
            var unit = GetString(row, headerMap, "unit")?.Trim();
            if (string.IsNullOrWhiteSpace(unit)) unit = "pza";

            var onHand = GetDecimal(row, headerMap, "onhand");
            var reorder = GetDecimal(row, headerMap, "reorder");
            var active = GetBool(row, headerMap, "active", defaultValue: true);
            var notes = GetString(row, headerMap, "notes")?.Trim() ?? "";

            // Normaliza
            name = name.Trim();
            category = category.Trim();
            model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
            location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            brandName = string.IsNullOrWhiteSpace(brandName) ? null : brandName.Trim();
            unit = unit.Trim();

            // Asegura catálogo categoría (si vino)
            if (!string.IsNullOrWhiteSpace(category))
            {
                var ck = category.ToLowerInvariant();
                if (!catByName.ContainsKey(ck))
                {
                    var c = new InventoryCategory
                    {
                        Name = category,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.InventoryCategories.Add(c);
                    catByName[ck] = c;
                }
            }

            // Asegura catálogo ubicación (si vino)
            if (!string.IsNullOrWhiteSpace(location))
            {
                var lk = location.ToLowerInvariant();
                if (!locByName.ContainsKey(lk))
                {
                    var l = new InventoryLocation
                    {
                        Name = location,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.InventoryLocations.Add(l);
                    locByName[lk] = l;
                }
            }

            // Asegura marca (si vino)
            Guid? brandId = null;
            if (!string.IsNullOrWhiteSpace(brandName))
            {
                var bk = brandName.ToLowerInvariant();
                if (!brandByName.TryGetValue(bk, out var b))
                {
                    b = new InventoryBrand
                    {
                        Name = brandName,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.InventoryBrands.Add(b);
                    brandByName[bk] = b;
                }
                brandId = b.Id;
            }

            // Match item
            InventoryItem? item = null;

            if (!string.IsNullOrWhiteSpace(sku))
            {
                var sk = sku.ToLowerInvariant();
                itemBySku.TryGetValue(sk, out item);
            }

            if (item == null)
            {
                var nk = name.ToLowerInvariant();
                itemByName.TryGetValue(nk, out item);
            }

            if (item == null)
            {
                item = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                };

                _db.InventoryItems.Add(item);
                created++;

                // cache por nombre y sku (si existe)
                itemByName[name.ToLowerInvariant()] = item;
                if (!string.IsNullOrWhiteSpace(sku))
                    itemBySku[sku.ToLowerInvariant()] = item;
            }
            else
            {
                updated++;
            }

            // Upsert fields
            item.Name = name;
            item.ModelCode = modelCode;
            item.Sku = sku;
            item.Category = category;          // texto seleccionado desde catálogo
            item.BrandId = brandId;            // FK a catálogo de marcas
            item.Model = model;
            item.Location = location;          // texto seleccionado desde catálogo

            item.Unit = unit;
            item.QuantityOnHand = onHand;
            item.ReorderLevel = reorder;
            item.IsActive = active;

            // Consumible vs hardware (si traes columna "tipo" en tu template viejo, aquí puedes mapearlo)
            // Si no viene, NO lo tocamos para no cambiar tu lógica:
            // item.IsConsumable = item.IsConsumable;

            item.Notes = notes;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        Info = $"Importación lista: {created} creados, {updated} actualizados, {skipped} saltados.";
        return RedirectToPage();
    }

    // =========================
    // Helpers
    // =========================

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var raw = headerRow.Cell(c).GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var k = NormalizeHeader(raw);

            // Aliases
            if (k is "nombre" or "name") map["name"] = c;
            else if (k is "idmodelo" or "id modelo" or "modeloid" or "modelcode" or "id modelo unico" or "id modelo único") map["modelcode"] = c;
            else if (k is "sku" or "codigo" or "código" or "clave") map["sku"] = c;
            else if (k is "categoria" or "categoría" or "category") map["category"] = c;
            else if (k is "marca" or "brand") map["brand"] = c;
            else if (k is "modelo" or "model") map["model"] = c;
            else if (k is "ubicacion" or "ubicación" or "location") map["location"] = c;
            else if (k is "unidad" or "unit") map["unit"] = c;
            else if (k is "existencia" or "onhand" or "on hand" or "qty" or "cantidad") map["onhand"] = c;
            else if (k is "stockminimo" or "stock mínimo" or "stock minimo" or "reorder" or "reorderlevel" or "min") map["reorder"] = c;
            else if (k is "activo" or "active") map["active"] = c;
            else if (k is "notas" or "notes" or "nota") map["notes"] = c;
        }

        return map;
    }

    private static string NormalizeHeader(string s)
    {
        s = s.Trim().ToLowerInvariant();
        s = s.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ü", "u");
        s = s.Replace("(", "").Replace(")", "");
        s = s.Replace("_", " ").Replace("-", " ");
        s = string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return s;
    }

    private static string? GetString(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col)) return null;
        return row.Cell(col).GetString();
    }

    private static decimal GetDecimal(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var col)) return 0m;
        var cell = row.Cell(col);

        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToDecimal(cell.GetDouble());
        }

        var txt = cell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(txt)) return 0m;

        // intenta con cultura actual y con invariant
        if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.CurrentCulture, out var d)) return d;
        if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;

        // intenta limpiar comas/espacios
        txt = txt.Replace(",", "").Replace(" ", "");
        if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;

        return 0m;
    }

    private static bool GetBool(IXLRow row, Dictionary<string, int> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var col)) return defaultValue;
        var cell = row.Cell(col);

        if (cell.DataType == XLDataType.Boolean)
            return cell.GetBoolean();

        var txt = cell.GetString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(txt)) return defaultValue;

        return txt is "true" or "1" or "si" or "sí" or "yes" or "y";
    }
}


