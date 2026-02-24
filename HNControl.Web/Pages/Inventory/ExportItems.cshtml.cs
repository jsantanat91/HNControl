using ClosedXML.Excel;
using HNControl.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class ExportItemsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ExportItemsModel(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync()
    {
        var rows = await _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Brand)
            .OrderBy(i => i.Name)
            .Select(i => new
            {
                i.Name,
                i.Sku,
                Brand = i.Brand != null ? i.Brand.Name : "",
                i.Model,
                i.Location,
                i.Category,
                // "Tipo" ahora se reporta como la categoría (evita que todo salga "Consumible")
                Type = i.Category ?? "",
                i.Unit,
                Existencia = i.QuantityOnHand,
                StockMinimo = i.ReorderLevel,
                i.IsActive,
                i.Notes
            })
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Items");

        // Headers (igual que la plantilla)
        ws.Cell(1, 1).Value = "Nombre";
        ws.Cell(1, 2).Value = "SKU";
        ws.Cell(1, 3).Value = "Marca";
        ws.Cell(1, 4).Value = "Modelo";
        ws.Cell(1, 5).Value = "Ubicacion";
        ws.Cell(1, 6).Value = "Categoria";
        ws.Cell(1, 7).Value = "Tipo";
        ws.Cell(1, 8).Value = "Unidad";
        ws.Cell(1, 9).Value = "Existencia";
        ws.Cell(1, 10).Value = "StockMinimo";
        ws.Cell(1, 11).Value = "Activo";
        ws.Cell(1, 12).Value = "Notas";

        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.Name;
            ws.Cell(r, 2).Value = x.Sku;
            ws.Cell(r, 3).Value = x.Brand;
            ws.Cell(r, 4).Value = x.Model;
            ws.Cell(r, 5).Value = x.Location;
            ws.Cell(r, 6).Value = x.Category;
            ws.Cell(r, 7).Value = x.Type;
            ws.Cell(r, 8).Value = x.Unit;
            ws.Cell(r, 9).Value = x.Existencia;
            ws.Cell(r, 10).Value = x.StockMinimo;
            ws.Cell(r, 11).Value = x.IsActive ? "Sí" : "No";
            ws.Cell(r, 12).Value = x.Notes;
            r++;
        }

        ws.Range(1, 1, 1, 12).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = $"inventario_items_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
