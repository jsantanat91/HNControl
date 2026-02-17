using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public record RowVm(
        string UserId,
        string Name,
        decimal SalaryMonthly,
        decimal BaseQuincenal,
        decimal Fixed80,
        decimal Variable20Max,
        decimal VariablePercent,
        decimal VariableMoney,
        decimal TotalQuincena
    );

    public List<RowVm> Rows { get; set; } = new();

    public string LabelsJson { get; set; } = "[]";
    public string ValuesJson { get; set; } = "[]";

    public async Task OnGetAsync(DateTime? start = null)
    {
        (PeriodStart, PeriodEnd) = GetCurrentQuincenaUtc(start);

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();

        var reviews = await _db.PerformanceReviews
            .Where(r => r.PeriodStart == PeriodStart && r.PeriodEnd == PeriodEnd)
            .ToListAsync();

        Rows = emps.Select(e =>
        {
            var r = reviews.FirstOrDefault(x => x.UserId == e.UserId);
            var baseQ = e.SalaryBase / 2m;
            var fixed80 = baseQ * 0.80m;
            var varMax = baseQ * 0.20m;
            var vp = r?.VariablePercent ?? 0m;
            var varMoney = varMax * vp;
            return new RowVm(e.UserId, e.FullName, e.SalaryBase, baseQ, fixed80, varMax, vp, varMoney, fixed80 + varMoney);
        }).ToList();

        LabelsJson = JsonSerializer.Serialize(Rows.Select(r => r.Name).ToList());
        ValuesJson = JsonSerializer.Serialize(Rows.Select(r => Math.Round(r.VariablePercent * 100m, 2)).ToList());
    }

    private static (DateTime start, DateTime end) GetCurrentQuincenaUtc(DateTime? start)
    {
        var d = (start ?? DateTime.Now).Date;
        if (d.Day <= 15)
            return (TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 1)),
                    TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 15)));

        return (TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 16)),
                TimeUtil.UtcDate(new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month))));
    }
}
