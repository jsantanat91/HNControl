using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Viaticos;

[Authorize(Roles = AppRoles.Admin)]
public class ExportWeekModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ExportWeekModel(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var week = await _db.ViaticWeeks
            .Include(w => w.EmployeeProfile)
            .Include(w => w.Entries).ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (week == null) return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("Empleado;SemanaInicio;Status;Total;Facturable;Dia;Categoria;Descripcion;Monto;EsFacturable;TienePDF;Archivo");

        foreach (var e in week.Entries.OrderBy(x => x.DayDate).ThenBy(x => x.Category))
        {
            sb.AppendLine(string.Join(";",
                Esc(week.EmployeeProfile?.FullName ?? week.UserId),
                week.WeekStartDate.ToString("yyyy-MM-dd"),
                week.Status.ToString(),
                week.TotalAmount.ToString("0.00"),
                week.BillableAmount.ToString("0.00"),
                e.DayDate.ToString("yyyy-MM-dd"),
                e.Category.ToString(),
                Esc(e.Description),
                e.Amount.ToString("0.00"),
                e.IsBillable ? "1" : "0",
                e.Attachment != null ? "1" : "0",
                Esc(e.Attachment?.OriginalFileName ?? "")
            ));
        }

        // Footer resumen
        sb.AppendLine($";;;;;;;;Total semana;{week.TotalAmount:0.00};;;");
        sb.AppendLine($";;;;;;;;Facturable;{week.BillableAmount:0.00};;;");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fn = $"viaticos_{(week.EmployeeProfile?.FullName ?? "empleado").Replace(" ", "_")}_{week.WeekStartDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fn);
    }

    private static string Esc(string s)
    {
        s ??= "";
        s = s.Replace("\"", "\"\"");
        if (s.Contains(';') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s}\"";
        return s;
    }
}
