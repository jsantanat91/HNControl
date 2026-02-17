using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
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
        // ✅ Siempre UTC para timestamptz
        var (periodStart, periodEnd) = ResolvePeriodUtc(start, end);

        var emps = await _db.EmployeeProfiles
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        // Reviews exactas del periodo (si no hay, variable=0)
        var reviews = await _db.PerformanceReviews
            .Where(r => r.PeriodStart == periodStart && r.PeriodEnd == periodEnd)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Nomina");

        ws.Cell(1, 1).Value = "Empleado";
        ws.Cell(1, 2).Value = "Puesto";
        ws.Cell(1, 3).Value = "Periodo";
        ws.Cell(1, 4).Value = "Sueldo Mensual";
        ws.Cell(1, 5).Value = "Base Quincenal";
        ws.Cell(1, 6).Value = "80% Fijo";
        ws.Cell(1, 7).Value = "20% Max";
        ws.Cell(1, 8).Value = "Variable %";
        ws.Cell(1, 9).Value = "Variable $";
        ws.Cell(1, 10).Value = "Total Quincenal";

        var row = 2;

        foreach (var e in emps)
        {
            var r = reviews.FirstOrDefault(x => x.UserId == e.UserId);
            var variablePct = r?.VariablePercent ?? 0m;

            var baseQ = e.SalaryBase / 2m;
            var fixed80 = baseQ * 0.80m;
            var max20 = baseQ * 0.20m;
            var varMoney = max20 * variablePct;
            var total = fixed80 + varMoney;

            ws.Cell(row, 1).Value = e.FullName;
            ws.Cell(row, 2).Value = e.Position;
            ws.Cell(row, 3).Value = $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
            ws.Cell(row, 4).Value = e.SalaryBase;
            ws.Cell(row, 5).Value = baseQ;
            ws.Cell(row, 6).Value = fixed80;
            ws.Cell(row, 7).Value = max20;
            ws.Cell(row, 8).Value = variablePct; // 0..1
            ws.Cell(row, 9).Value = varMoney;
            ws.Cell(row, 10).Value = total;

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

    private static (DateTime start, DateTime end) ResolvePeriodUtc(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue)
            return (TimeUtil.UtcDate(start.Value), TimeUtil.UtcDate(end.Value));

        // default: quincena actual
        var now = DateTime.Now.Date;
        if (now.Day <= 15)
            return (TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 1)), TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 15)));

        return (TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 16)),
                TimeUtil.UtcDate(new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month))));
    }
}
