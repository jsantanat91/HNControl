using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class RateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public RateModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public SelectList EmployeeItems { get; set; } = default!;
    public string? Error { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required] public string UserId { get; set; } = "";

        // Date-only, pero SIEMPRE en UTC para Postgres timestamptz.
        [DataType(DataType.Date)] public DateTime PeriodStart { get; set; } = TimeUtil.UtcDate(DateTime.UtcNow.AddDays(-14));
        [DataType(DataType.Date)] public DateTime PeriodEnd { get; set; } = TimeUtil.UtcDate(DateTime.UtcNow);

        [Range(1, 5)] public int PersonalPerformance { get; set; } = 3;
        [Range(1, 5)] public int Teamwork { get; set; } = 3;
        [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
        [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
        [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
        [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

        [MaxLength(600)] public string Notes { get; set; } = "";
    }

    public async Task OnGetAsync(string? userId, DateTime? start = null, DateTime? end = null)
    {
        await LoadEmployeesAsync();

        if (!string.IsNullOrWhiteSpace(userId))
            Input.UserId = userId;

        if (start.HasValue) Input.PeriodStart = TimeUtil.UtcDate(start.Value);
        if (end.HasValue) Input.PeriodEnd = TimeUtil.UtcDate(end.Value);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadEmployeesAsync();
        if (!ModelState.IsValid) return Page();

        var ps = TimeUtil.UtcDate(Input.PeriodStart);
        var pe = TimeUtil.UtcDate(Input.PeriodEnd);

        if (pe < ps)
        {
            Error = "La fecha fin no puede ser menor al inicio.";
            return Page();
        }

        var existing = await _db.PerformanceReviews
            .FirstOrDefaultAsync(r =>
                r.UserId == Input.UserId &&
                r.PeriodStart == ps &&
                r.PeriodEnd == pe);

        var avg = (Input.PersonalPerformance + Input.Teamwork + Input.PunctualityAttendance +
                   Input.ProjectExecution + Input.OrderCleanliness + Input.TechnicalSkills) / 6m;

        var variablePercent = Math.Round(avg / 5m, 4); // 0..1
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        if (existing == null)
        {
            _db.PerformanceReviews.Add(new PerformanceReview
            {
                UserId = Input.UserId,
                PeriodStart = ps,
                PeriodEnd = pe,

                PersonalPerformance = Input.PersonalPerformance,
                Teamwork = Input.Teamwork,
                PunctualityAttendance = Input.PunctualityAttendance,
                ProjectExecution = Input.ProjectExecution,
                OrderCleanliness = Input.OrderCleanliness,
                TechnicalSkills = Input.TechnicalSkills,

                Notes = (Input.Notes ?? "").Trim(),
                RatedByUserId = adminId,
                RatedAt = DateTime.UtcNow,
                VariablePercent = variablePercent
            });
        }
        else
        {
            existing.PersonalPerformance = Input.PersonalPerformance;
            existing.Teamwork = Input.Teamwork;
            existing.PunctualityAttendance = Input.PunctualityAttendance;
            existing.ProjectExecution = Input.ProjectExecution;
            existing.OrderCleanliness = Input.OrderCleanliness;
            existing.TechnicalSkills = Input.TechnicalSkills;

            existing.Notes = (Input.Notes ?? "").Trim();
            existing.RatedByUserId = adminId;
            existing.RatedAt = DateTime.UtcNow;
            existing.VariablePercent = variablePercent;
        }

        await _db.SaveChangesAsync();

        // regresa al índice admin, apuntando a la misma quincena
        return RedirectToPage("/Admin/Performance/Index", new { start = ps });
    }

    private async Task LoadEmployeesAsync()
    {
        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(emps, "UserId", "FullName");
    }
}
