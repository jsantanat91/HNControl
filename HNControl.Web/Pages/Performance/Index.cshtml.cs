using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Performance;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record AdminRow(
        string UserId,
        string Employee,
        string Position,
        decimal SalaryBase,
        string LastPeriod,
        decimal VariablePercent,
        decimal TotalPay
    );

    public List<AdminRow> AdminRows { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (!User.IsInRole(AppRoles.Admin)) return;

        var employees = await _db.EmployeeProfiles
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var latestReviews = await _db.PerformanceReviews
            .GroupBy(r => r.UserId)
            .Select(g => g.OrderByDescending(x => x.PeriodStart).First())
            .ToListAsync();

        foreach (var e in employees)
        {
            var lr = latestReviews.FirstOrDefault(x => x.UserId == e.UserId);

            var variablePct = lr?.VariablePercent ?? 0m;
            var base80 = e.SalaryBase * 0.80m;
            var var20 = (e.SalaryBase * 0.20m) * variablePct;
            var total = base80 + var20;

            AdminRows.Add(new AdminRow(
                e.UserId,
                e.FullName,
                e.Position,
                e.SalaryBase,
                lr == null ? "—" : $"{lr.PeriodStart:yyyy-MM-dd} a {lr.PeriodEnd:yyyy-MM-dd}",
                variablePct,
                total
            ));
        }
    }
}
