using HNControl.Web.Data;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Inventory.Items;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<InventoryItem> Items { get; set; } = new();

    [TempData] public string? Info { get; set; }
    [TempData] public string? Error { get; set; }

    

    public IActionResult OnGetTemplate()
    {
        // Plantilla para carga masiva
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Items");

        ws.Cell(1, 1).Value = "Nombre";
        ws.Cell(1, 2).Value = "SKU";
        ws.Cell(1, 3).Value = "Categoría";
        ws.Cell(1, 4).Value = "Tipo";      // Consumible | Hardware
        ws.Cell(1, 5).Value = "Unidad";    // pza, m, caja...
        ws.Cell(1, 6).Value = "OnHand";    // existencia
        ws.Cell(1, 7).Value = "Reorder";   // reorden
        ws.Cell(1, 8).Value = "Activo";    // Sí | No
        ws.Cell(1, 9).Value = "Notas";

        ws.Range(1, 1, 1, 9).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        // Validaciones (para que no te metan "HardWaree" y luego culpen a la app 😄)
        var dvTipo = ws.Range(2, 4, 2000, 4).SetDataValidation();
        dvTipo.IgnoreBlanks = true;
        dvTipo.InCellDropdown = true;
        dvTipo.List("Consumible,Hardware", true);

        var dvActivo = ws.Range(2, 8, 2000, 8).SetDataValidation();
        dvActivo.IgnoreBlanks = true;
        dvActivo.InCellDropdown = true;
        dvActivo.List("Sí,No", true);

        // Anchos razonables
        ws.Columns(1, 9).AdjustToContents();
        ws.Column(1).Width = Math.Max(ws.Column(1).Width, 32);
        ws.Column(9).Width = Math.Max(ws.Column(9).Width, 40);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = "inventario_template_import.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnPostImportAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            Error = "Selecciona un archivo .xlsx.";
            return RedirectToPage(new { q = Q });
        }

        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (ext != ".xlsx")
        {
            Error = "Solo se permiten archivos .xlsx (Excel).";
            return RedirectToPage(new { q = Q });
        }

        // Cache de items existentes (SKU y Nombre)
        var existing = await _db.InventoryItems.ToListAsync();
        var bySku = existing
            .Where(i => !string.IsNullOrWhiteSpace(i.Sku))
            .GroupBy(i => i.Sku.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var byName = existing
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .GroupBy(i => i.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        int created = 0, updated = 0, skipped = 0;
        var errors = new List<string>();

        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
        {
            Error = "No pude leer la hoja del Excel.";
            return RedirectToPage(new { q = Q });
        }

        // Esperamos headers en la fila 1 (como la plantilla)
        // Cols: 1 Nombre, 2 SKU, 3 Categoría, 4 Tipo, 5 Unidad, 6 OnHand, 7 Reorder, 8 Activo, 9 Notas
        for (var row = 2; row <= 5000; row++)
        {
            var name = (ws.Cell(row, 1).GetString() ?? "").Trim();
            var sku = (ws.Cell(row, 2).GetString() ?? "").Trim();
            var category = (ws.Cell(row, 3).GetString() ?? "").Trim();
            var tipo = (ws.Cell(row, 4).GetString() ?? "").Trim();
            var unit = (ws.Cell(row, 5).GetString() ?? "").Trim();
            var activeTxt = (ws.Cell(row, 8).GetString() ?? "").Trim();
            var notes = (ws.Cell(row, 9).GetString() ?? "").Trim();

            // Si la fila está vacía, corta
            var empty = string.IsNullOrWhiteSpace(name)
                        && string.IsNullOrWhiteSpace(sku)
                        && string.IsNullOrWhiteSpace(category)
                        && string.IsNullOrWhiteSpace(tipo)
                        && string.IsNullOrWhiteSpace(unit)
                        && string.IsNullOrWhiteSpace(activeTxt)
                        && string.IsNullOrWhiteSpace(notes)
                        && ws.Cell(row, 6).IsEmpty()
                        && ws.Cell(row, 7).IsEmpty();

            if (empty) break;

            if (string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                errors.Add($"Fila {row}: 'Nombre' es requerido.");
                continue;
            }

            decimal onHand = 0m;
            decimal reorder = 0m;

            try
            {
                // Soporta número o texto
                var c6 = ws.Cell(row, 6);
                if (!c6.IsEmpty())
                    onHand = c6.DataType == XLDataType.Number ? (decimal)c6.GetDouble() : decimal.Parse(c6.GetString());

                var c7 = ws.Cell(row, 7);
                if (!c7.IsEmpty())
                    reorder = c7.DataType == XLDataType.Number ? (decimal)c7.GetDouble() : decimal.Parse(c7.GetString());
            }
            catch
            {
                skipped++;
                errors.Add($"Fila {row}: OnHand/Reorder inválidos (usa números).");
                continue;
            }

            bool isConsumable = true;
            if (!string.IsNullOrWhiteSpace(tipo))
            {
                var t = tipo.Trim().ToLowerInvariant();
                isConsumable = t.StartsWith("c"); // consumible
                if (t.StartsWith("h")) isConsumable = false; // hardware
            }

            bool isActive = true;
            if (!string.IsNullOrWhiteSpace(activeTxt))
            {
                var a = activeTxt.Trim().ToLowerInvariant();
                if (a.StartsWith("n")) isActive = false; // no
                if (a.StartsWith("s")) isActive = true;  // sí
            }

            unit = string.IsNullOrWhiteSpace(unit) ? "pza" : unit;

            InventoryItem item;
            bool isNew = false;

            var skuKey = (sku ?? "").Trim().ToLowerInvariant();
            var nameKey = name.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(skuKey) && bySku.TryGetValue(skuKey, out item!))
            {
                // update por SKU
            }
            else if (byName.TryGetValue(nameKey, out item!))
            {
                // update por nombre (fallback)
            }
            else
            {
                item = new InventoryItem
                {
                    CreatedAt = DateTime.UtcNow
                };
                isNew = true;
            }

            // Limita tamaños (por si alguien pega una novela)
            string cut(string s, int max) => (s ?? "").Length <= max ? (s ?? "") : (s ?? "").Substring(0, max);

            item.Name = cut(name, 200);
            item.Sku = cut(sku, 60);
            item.Category = cut(category, 100);
            item.IsConsumable = isConsumable;
            item.Unit = cut(unit, 40);
            item.QuantityOnHand = onHand;
            item.ReorderLevel = reorder;
            item.IsActive = isActive;
            item.Notes = cut(notes, 2000);
            item.UpdatedAt = DateTime.UtcNow;

            if (isNew)
            {
                _db.InventoryItems.Add(item);
                created++;

                // actualiza diccionarios
                if (!string.IsNullOrWhiteSpace(item.Sku))
                    bySku[item.Sku.Trim().ToLowerInvariant()] = item;
                byName[item.Name.Trim().ToLowerInvariant()] = item;
            }
            else
            {
                updated++;
            }
        }

        await _db.SaveChangesAsync();

        var errHead = errors.Count > 0
            ? $" · Errores: {errors.Count} (primeros: {string.Join(" | ", errors.Take(5))})"
            : "";

        Info = $"Importación lista. Nuevos: {created} · Actualizados: {updated} · Omitidos: {skipped}{errHead}";
        return RedirectToPage(new { q = Q });
    }


    public async Task OnGetAsync()
    {
        var q = (Q ?? "").Trim();
        var query = _db.InventoryItems.AsNoTracking().OrderBy(i => i.Name).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var l = q.ToLower();
            query = query.Where(i => (i.Name ?? "").ToLower().Contains(l) || (i.Sku ?? "").ToLower().Contains(l));
        }
        Items = await query.Take(1000).ToListAsync();
    }
}