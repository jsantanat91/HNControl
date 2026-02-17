using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Viaticos;

public class ExportWeekModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ExportWeekModel(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var week = await _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.Entries)
                .ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (week == null) return NotFound();

        // Recomendación: exportar solo si Approved
        if (week.Status != ViaticWeekStatus.Approved)
            return BadRequest("Primero aprueba la semana para exportar.");

        var emp = week.EmployeeProfile?.FullName ?? week.UserId;
        var total = week.Entries.Sum(e => e.Amount);
        var billable = week.Entries.Where(e => e.IsBillable).Sum(e => e.Amount);
        var nonBillable = total - billable;

        decimal SumCat(ViaticCategory c) => week.Entries.Where(e => e.Category == c).Sum(e => e.Amount);

        using var wb = new XLWorkbook();

        // Hoja 1: Resumen
        var ws1 = wb.Worksheets.Add("Resumen");
        ws1.Cell(1, 1).Value = "Empleado";
        ws1.Cell(1, 2).Value = emp;
        ws1.Cell(2, 1).Value = "Semana (lunes)";
        ws1.Cell(2, 2).Value = week.WeekStartDate.ToString("yyyy-MM-dd");
        ws1.Cell(3, 1).Value = "Status";
        ws1.Cell(3, 2).Value = week.Status.ToString();
        ws1.Cell(4, 1).Value = "Total";
        ws1.Cell(4, 2).Value = total;
        ws1.Cell(5, 1).Value = "Facturable";
        ws1.Cell(5, 2).Value = billable;
        ws1.Cell(6, 1).Value = "No facturable";
        ws1.Cell(6, 2).Value = nonBillable;

        ws1.Cell(8, 1).Value = "Transporte"; ws1.Cell(8, 2).Value = SumCat(ViaticCategory.Transporte);
        ws1.Cell(9, 1).Value = "Gasolina"; ws1.Cell(9, 2).Value = SumCat(ViaticCategory.Gasolina);
        ws1.Cell(10, 1).Value = "Material"; ws1.Cell(10, 2).Value = SumCat(ViaticCategory.Material);
        ws1.Cell(11, 1).Value = "Otros"; ws1.Cell(11, 2).Value = SumCat(ViaticCategory.Otros);

        ws1.Columns().AdjustToContents();

        // Hoja 2: Detalle
        var ws2 = wb.Worksheets.Add("Detalle");
        ws2.Cell(1, 1).Value = "Día";
        ws2.Cell(1, 2).Value = "Categoría";
        ws2.Cell(1, 3).Value = "Descripción";
        ws2.Cell(1, 4).Value = "Monto";
        ws2.Cell(1, 5).Value = "Facturable";
        ws2.Cell(1, 6).Value = "Tiene PDF";

        var row = 2;
        foreach (var e in week.Entries.OrderBy(x => x.DayDate).ThenBy(x => x.Category))
        {
            ws2.Cell(row, 1).Value = e.DayDate.ToString("yyyy-MM-dd");
            ws2.Cell(row, 2).Value = e.Category.ToString();
            ws2.Cell(row, 3).Value = e.Description;
            ws2.Cell(row, 4).Value = e.Amount;
            ws2.Cell(row, 5).Value = e.IsBillable ? "Sí" : "No";
            ws2.Cell(row, 6).Value = e.Attachment != null ? "Sí" : "No";
            row++;
        }

        ws2.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var fileName = $"viaticos_{Sanitize(emp)}_{week.WeekStartDate:yyyyMMdd}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
