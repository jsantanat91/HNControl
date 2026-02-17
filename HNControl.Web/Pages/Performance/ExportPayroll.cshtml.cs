using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class ExportPayrollModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ExportPayrollModel(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync(DateTime? start, DateTime? end)
    {
        var periodStart = (start ?? DateTime.Today.AddDays(-14)).Date;
        var periodEnd = (end ?? DateTime.Today).Date;

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();

        // Reviews exactas del periodo (si no hay, variable=0)
        var reviews = await _db.PerformanceReviews
            .Where(r => r.PeriodStart == periodStart && r.PeriodEnd == periodEnd)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Nomina");

        ws.Cell(1, 1).Value = "Empleado";
        ws.Cell(1, 2).Value = "Puesto";
        ws.Cell(1, 3).Value = "Periodo";
        ws.Cell(1, 4).Value = "Sueldo Base";
        ws.Cell(1, 5).Value = "80% Fijo";
        ws.Cell(1, 6).Value = "20% Max";
        ws.Cell(1, 7).Value = "Variable %";
        ws.Cell(1, 8).Value = "Variable $";
        ws.Cell(1, 9).Value = "Total Quincenal";

        var row = 2;

        foreach (var e in emps)
        {
            var r = reviews.FirstOrDefault(x => x.UserId == e.UserId);
            var variablePct = r?.VariablePercent ?? 0m;

            var base80 = e.SalaryBase * 0.80m;
            var max20 = e.SalaryBase * 0.20m;
            var varMoney = max20 * variablePct;
            var total = base80 + varMoney;

            ws.Cell(row, 1).Value = e.FullName;
            ws.Cell(row, 2).Value = e.Position;
            ws.Cell(row, 3).Value = $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
            ws.Cell(row, 4).Value = e.SalaryBase;
            ws.Cell(row, 5).Value = base80;
            ws.Cell(row, 6).Value = max20;
            ws.Cell(row, 7).Value = variablePct;
            ws.Cell(row, 8).Value = varMoney;
            ws.Cell(row, 9).Value = total;

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var fileName = $"nomina_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
