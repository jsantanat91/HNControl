using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Performance;

[Authorize]
public class MyModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public MyModel(ApplicationDbContext db) => _db = db;

    public string EmployeeName { get; set; } = "";
    public decimal SalaryBase { get; set; }

    public record ReviewRow(string Period, decimal VariablePercent, decimal TotalPay, string Notes);
    public List<ReviewRow> Reviews { get; set; } = new();

    public string ChartLabelsJson { get; set; } = "[]";
    public string ChartValuesJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync(string? userId)
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var targetUserId = isAdmin && !string.IsNullOrWhiteSpace(userId) ? userId : currentUserId;
        if (string.IsNullOrWhiteSpace(targetUserId)) return Forbid();

        var emp = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == targetUserId);
        if (emp == null) return NotFound();

        EmployeeName = emp.FullName;
        SalaryBase = emp.SalaryBase;

        var reviews = await _db.PerformanceReviews
            .Where(r => r.UserId == targetUserId)
            .OrderByDescending(r => r.PeriodStart)
            .Take(12)
            .ToListAsync();

        Reviews = reviews.Select(r =>
        {
            var baseQuincena = SalaryBase / 2m;
            var fijo80 = baseQuincena * 0.80m;
            var max20 = baseQuincena * 0.20m;
            var total = fijo80 + (max20 * r.VariablePercent);

            return new ReviewRow(
                $"{r.PeriodStart:yyyy-MM-dd} a {r.PeriodEnd:yyyy-MM-dd}",
                r.VariablePercent,
                total,
                (r.Notes ?? "").Trim()
            );
        }).ToList();

        var labels = reviews.OrderBy(r => r.PeriodStart).Select(r => r.PeriodStart.ToString("MM-dd")).ToList();
        var vals = reviews.OrderBy(r => r.PeriodStart).Select(r => (double)r.VariablePercent).ToList();

        ChartLabelsJson = JsonSerializer.Serialize(labels);
        ChartValuesJson = JsonSerializer.Serialize(vals);

        return Page();
    }
}
