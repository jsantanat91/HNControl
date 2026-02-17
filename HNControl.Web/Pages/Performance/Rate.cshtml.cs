using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class RateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public RateModel(ApplicationDbContext db) => _db = db;

    public SelectList EmployeeItems { get; set; } = default!;
    public string? Error { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required] public string UserId { get; set; } = "";

        [DataType(DataType.Date)] public DateTime PeriodStart { get; set; } = DateTime.Today.AddDays(-14);
        [DataType(DataType.Date)] public DateTime PeriodEnd { get; set; } = DateTime.Today;

        [Range(1, 5)] public int PersonalPerformance { get; set; } = 3;
        [Range(1, 5)] public int Teamwork { get; set; } = 3;
        [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
        [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
        [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
        [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

        [MaxLength(600)] public string Notes { get; set; } = "";
    }

    public async Task OnGetAsync(string? userId)
    {
        await LoadEmployeesAsync();

        if (!string.IsNullOrWhiteSpace(userId))
            Input.UserId = userId;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadEmployeesAsync();
        if (!ModelState.IsValid) return Page();

        if (Input.PeriodEnd.Date < Input.PeriodStart.Date)
        {
            Error = "La fecha fin no puede ser menor al inicio.";
            return Page();
        }

        // Upsert por periodo
        var existing = await _db.PerformanceReviews
            .FirstOrDefaultAsync(r =>
                r.UserId == Input.UserId &&
                r.PeriodStart.Date == Input.PeriodStart.Date &&
                r.PeriodEnd.Date == Input.PeriodEnd.Date);

        var avg = (Input.PersonalPerformance + Input.Teamwork + Input.PunctualityAttendance +
                   Input.ProjectExecution + Input.OrderCleanliness + Input.TechnicalSkills) / 6m;

        var variablePercent = Math.Round(avg / 5m, 4); // 0..1

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        if (existing == null)
        {
            _db.PerformanceReviews.Add(new PerformanceReview
            {
                UserId = Input.UserId,
                PeriodStart = Input.PeriodStart.Date,
                PeriodEnd = Input.PeriodEnd.Date,

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
        return RedirectToPage("/Performance/Index");
    }

    private async Task LoadEmployeesAsync()
    {
        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(emps, "UserId", "FullName");
    }
}
