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
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new
            {
                i.Name,
                i.Sku,
                Type = i.IsConsumable ? "Consumible" : "Hardware",
                i.Unit,
                i.QuantityOnHand,
                i.ReorderLevel,
                i.Notes
            })
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Items");

        ws.Cell(1, 1).Value = "Nombre";
        ws.Cell(1, 2).Value = "SKU";
        ws.Cell(1, 3).Value = "Tipo";
        ws.Cell(1, 4).Value = "Unidad";
        ws.Cell(1, 5).Value = "OnHand";
        ws.Cell(1, 6).Value = "Reorder";
        ws.Cell(1, 7).Value = "Notas";

        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.Name;
            ws.Cell(r, 2).Value = x.Sku;
            ws.Cell(r, 3).Value = x.Type;
            ws.Cell(r, 4).Value = x.Unit;
            ws.Cell(r, 5).Value = x.QuantityOnHand;
            ws.Cell(r, 6).Value = x.ReorderLevel;
            ws.Cell(r, 7).Value = x.Notes;
            r++;
        }

        ws.Range(1, 1, 1, 7).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = $"inventario_items_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
