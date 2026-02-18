using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    public EmployeeProfile? Employee { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required] public string UserId { get; set; } = "";
        [Required] public DateTime PeriodStart { get; set; }
        [Required] public DateTime PeriodEnd { get; set; }

        [Range(1, 5)] public int PersonalPerformance { get; set; } = 3;
        [Range(1, 5)] public int Teamwork { get; set; } = 3;
        [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
        [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
        [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
        [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

        [MaxLength(1200)] public string Notes { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string userId, DateTime? start)
    {
        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (Employee == null) return NotFound();

        (PeriodStart, PeriodEnd) = GetQuincenaUtc(start);

        var existing = await _db.PerformanceReviews
            .Where(r => r.UserId == userId
                        && r.PeriodStart >= PeriodStart && r.PeriodStart < PeriodStart.AddDays(1)
                        && r.PeriodEnd >= PeriodEnd && r.PeriodEnd < PeriodEnd.AddDays(1))
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            Input = new InputModel
            {
                UserId = existing.UserId,
                PeriodStart = existing.PeriodStart,
                PeriodEnd = existing.PeriodEnd,
                PersonalPerformance = existing.PersonalPerformance,
                Teamwork = existing.Teamwork,
                PunctualityAttendance = existing.PunctualityAttendance,
                ProjectExecution = existing.ProjectExecution,
                OrderCleanliness = existing.OrderCleanliness,
                TechnicalSkills = existing.TechnicalSkills,
                Notes = existing.Notes
            };
        }
        else
        {
            Input.UserId = userId;
            Input.PeriodStart = PeriodStart;
            Input.PeriodEnd = PeriodEnd;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == Input.UserId);
        if (Employee == null) return NotFound();

        var ps = TimeUtil.UtcDate(Input.PeriodStart);
        var pe = TimeUtil.UtcDate(Input.PeriodEnd);

        var r = await _db.PerformanceReviews
            .Where(x => x.UserId == Input.UserId
                        && x.PeriodStart >= ps && x.PeriodStart < ps.AddDays(1)
                        && x.PeriodEnd >= pe && x.PeriodEnd < pe.AddDays(1))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        if (r == null)
        {
            r = new PerformanceReview
            {
                UserId = Input.UserId,
                PeriodStart = ps,
                PeriodEnd = pe,
                CreatedAt = DateTime.UtcNow
            };
            _db.PerformanceReviews.Add(r);
        }
        else
        {
            r.PeriodStart = ps;
            r.PeriodEnd = pe;
        }

        r.PersonalPerformance = Input.PersonalPerformance;
        r.Teamwork = Input.Teamwork;
        r.PunctualityAttendance = Input.PunctualityAttendance;
        r.ProjectExecution = Input.ProjectExecution;
        r.OrderCleanliness = Input.OrderCleanliness;
        r.TechnicalSkills = Input.TechnicalSkills;
        r.Notes = (Input.Notes ?? "").Trim();

        r.Recalc();
        await _db.SaveChangesAsync();

        return RedirectToPage("/Admin/Performance/Index", new { start = ps.ToString("yyyy-MM-dd") });
    }

    private static (DateTime start, DateTime end) GetQuincenaUtc(DateTime? start)
    {
        var d = (start ?? DateTime.Now).Date;
        if (d.Day <= 15)
            return (TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 1)),
                    TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 15)));

        return (TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 16)),
                TimeUtil.UtcDate(new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month))));
    }
}
