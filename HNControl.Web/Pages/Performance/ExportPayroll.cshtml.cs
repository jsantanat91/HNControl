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

        // 1) Fuente principal: EmployeeProfiles
        var emps = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();

        // 2) Fallback: si por alguna razón en esta BD/entorno no hay EmployeeProfiles,
        // al menos exportamos los usuarios con rol Admin/Employee (para no mandar Excel vacío).
        if (emps.Count == 0)
        {
            var roleIds = await _db.Roles
                .Where(r => r.Name == AppRoles.Admin || r.Name == AppRoles.Employee)
                .Select(r => r.Id)
                .ToListAsync();

            var userIds = await _db.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .OrderBy(u => u.Email)
                .ToListAsync();

            emps = users.Select(u => new EmployeeProfile
            {
                UserId = u.Id,
                FullName = (u.UserName ?? u.Email ?? u.Id).Trim(),
                Email = (u.Email ?? "").Trim(),
                Nss = "",
                Position = "",
                Phone = "",
                Gender = "",
                SalaryBase = 0m
            }).ToList();
        }

        // Reviews del periodo (robusto contra timestamps con hora distinta)
        var reviews = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r =>
                r.PeriodStart >= periodStart && r.PeriodStart < periodStart.AddDays(1) &&
                r.PeriodEnd >= periodEnd && r.PeriodEnd < periodEnd.AddDays(1))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

        var byUser = reviews
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Nomina");

        ws.Cell(1, 1).Value = "Empleado";
        ws.Cell(1, 2).Value = "Correo";
        ws.Cell(1, 3).Value = "NSS";
        ws.Cell(1, 4).Value = "Puesto";
        ws.Cell(1, 5).Value = "Periodo";
        ws.Cell(1, 6).Value = "Sueldo Mensual";
        ws.Cell(1, 7).Value = "Base Quincenal";
        ws.Cell(1, 8).Value = "80% Fijo";
        ws.Cell(1, 9).Value = "20% Max";
        ws.Cell(1, 10).Value = "Variable %";
        ws.Cell(1, 11).Value = "Variable $";
        ws.Cell(1, 12).Value = "Total Quincenal (sin ajustes)";
        ws.Cell(1, 13).Value = "Deducciones";
        ws.Cell(1, 14).Value = "Bonos";
        ws.Cell(1, 15).Value = "Neto Quincenal";

        var row = 2;

        foreach (var e in emps)
        {
            byUser.TryGetValue(e.UserId, out var r);
            var variablePct = r?.VariablePercent ?? 0m; // 0..1

            var baseQ = e.SalaryBase / 2m;
            var fixed80 = baseQ * 0.80m;
            var max20 = baseQ * 0.20m;
            var varMoney = max20 * variablePct;
            var total = fixed80 + varMoney;

            var (deductions, bonuses) = await CalcPayrollAdjustmentsAsync(e.UserId, baseQ, total, periodStart, periodEnd);
            var net = Math.Max(0m, Math.Round(total - deductions + bonuses, 2));

            ws.Cell(row, 1).Value = e.FullName;
            ws.Cell(row, 2).Value = e.Email;
            ws.Cell(row, 3).Value = e.Nss;
            ws.Cell(row, 4).Value = e.Position;
            ws.Cell(row, 5).Value = $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
            ws.Cell(row, 6).Value = e.SalaryBase;
            ws.Cell(row, 7).Value = baseQ;
            ws.Cell(row, 8).Value = fixed80;
            ws.Cell(row, 9).Value = max20;
            ws.Cell(row, 10).Value = variablePct;
            ws.Cell(row, 11).Value = varMoney;
            ws.Cell(row, 12).Value = total;
            ws.Cell(row, 13).Value = deductions;
            ws.Cell(row, 14).Value = bonuses;
            ws.Cell(row, 15).Value = net;

            row++;
        }

        ws.Range(1, 1, 1, 15).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);

        ws.Column(6).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(7).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(8).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(9).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(10).Style.NumberFormat.Format = "0.00%";
        ws.Column(11).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(12).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(13).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(14).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(15).Style.NumberFormat.Format = "#,##0.00";

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
            return (TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 1)),
                    TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 15)));

        return (TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 16)),
                TimeUtil.UtcDate(new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month))));
    }

    private async Task<(decimal deductions, decimal bonuses)> CalcPayrollAdjustmentsAsync(
        string userId,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodStart,
        DateTime periodEnd)
    {
        try
        {
            var periodDate = periodEnd.Date;
            var active = await _db.EmployeeDeductions
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Where(d => d.StartDate <= periodDate && (d.EndDate == null || d.EndDate >= periodDate))
                .ToListAsync();

            var result = PayrollDeductionMath.CalculateTotals(active, baseQuincenal, estimatedQuincenal, periodDate);
            return (result.deductions, result.bonuses);
        }
        catch
        {
            return (0m, 0m);
        }
    }
}
