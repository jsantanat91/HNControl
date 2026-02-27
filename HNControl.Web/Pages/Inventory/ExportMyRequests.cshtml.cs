using System.Security.Claims;
using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class ExportMyRequestsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ExportMyRequestsModel(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var rows = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .Where(m => m.RequestedByUserId == userId || m.ResponsibleUserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .Take(3000)
            .ToListAsync();

        // Mapeo orden -> anchorId para que el Excel tenga “orden” consistente.
        var groups = rows
            .GroupBy(m => new { m.RequestedAt, m.RequestedByUserId, m.Type, m.ProjectId, m.ResponsibleUserId })
            .OrderByDescending(g => g.Key.RequestedAt)
            .ToList();

        var orderAnchorMap = groups.ToDictionary(
            g => g.Key,
            g => g.OrderBy(x => x.Id).First().Id
        );

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Solicitudes");

        ws.Cell(1, 1).Value = "Orden (ancla)";
        ws.Cell(1, 2).Value = "Fecha";
        ws.Cell(1, 3).Value = "Tipo";
        ws.Cell(1, 4).Value = "Item";
        ws.Cell(1, 5).Value = "SKU";
        ws.Cell(1, 6).Value = "Cantidad";
        ws.Cell(1, 7).Value = "Unidad";
        ws.Cell(1, 8).Value = "Proyecto";
        ws.Cell(1, 9).Value = "Responsable";
        ws.Cell(1, 10).Value = "Cliente (HW)";
        ws.Cell(1, 11).Value = "Serie";
        ws.Cell(1, 12).Value = "Status";
        ws.Cell(1, 13).Value = "Notas";

        var r = 2;
        foreach (var m in rows)
        {
            var key = new { m.RequestedAt, m.RequestedByUserId, m.Type, m.ProjectId, m.ResponsibleUserId };
            var anchorId = orderAnchorMap.TryGetValue(key, out var a) ? a : m.Id;

            ws.Cell(r, 1).Value = anchorId.ToString();
            ws.Cell(r, 2).Value = m.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            ws.Cell(r, 3).Value = m.Type == InventoryMovementType.In ? "Entrada" : "Salida";
            ws.Cell(r, 4).Value = m.Item?.Name;
            ws.Cell(r, 5).Value = m.Item?.Sku;
            ws.Cell(r, 6).Value = m.Quantity;
            ws.Cell(r, 7).Value = m.Item?.Unit;
            ws.Cell(r, 8).Value = m.Project?.Title;
            ws.Cell(r, 9).Value = m.ResponsibleName;
            ws.Cell(r, 10).Value = m.AssignedClient?.Name;
            ws.Cell(r, 11).Value = m.SerialNumber;
            ws.Cell(r, 12).Value = m.Status.ToString();
            ws.Cell(r, 13).Value = m.Notes;
            r++;
        }

        ws.Range(1, 1, 1, 13).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var fileName = $"inventario_mis_solicitudes_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
