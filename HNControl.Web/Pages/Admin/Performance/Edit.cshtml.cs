using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
        [Range(1, 5)] public int ParticipationInTeam { get; set; } = 3;
        [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
        [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
        [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
        [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

        [MaxLength(3600)] public string Notes { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string userId, DateTime? start)
    {
        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (Employee == null) return NotFound();

        (PeriodStart, PeriodEnd) = GetQuincenaUtc(start);

        var psMin = PeriodStart.AddDays(-1);
        var psMax = PeriodStart.AddDays(2);
        var peMin = PeriodEnd.AddDays(-1);
        var peMax = PeriodEnd.AddDays(2);

        var existing = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId
                     && r.PeriodStart >= psMin && r.PeriodStart < psMax
                     && r.PeriodEnd >= peMin && r.PeriodEnd < peMax)
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
                ParticipationInTeam = existing.ParticipationInTeam,
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

        var notes = (Input.Notes ?? "").Trim();
        var sum = (decimal)(Input.PersonalPerformance + Input.Teamwork + Input.ParticipationInTeam + Input.PunctualityAttendance +
                            Input.ProjectExecution + Input.OrderCleanliness + Input.TechnicalSkills);

        var variablePercent = Math.Round(sum / 35m, 4);
        variablePercent = Math.Clamp(variablePercent, 0m, 1m);

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var now = DateTime.UtcNow;

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""PerformanceReviews"" (
    ""Id"", ""UserId"", ""PeriodStart"", ""PeriodEnd"",
    ""PersonalPerformance"", ""Teamwork"", ""ParticipationInTeam"", ""PunctualityAttendance"", ""ProjectExecution"", ""OrderCleanliness"", ""TechnicalSkills"",
    ""VariablePercent"", ""Notes"", ""RatedByUserId"", ""RatedAt"", ""CreatedAt"", ""UpdatedAt""
) VALUES (
    {Guid.NewGuid()}, {Input.UserId}, {ps}, {pe},
    {Input.PersonalPerformance}, {Input.Teamwork}, {Input.ParticipationInTeam}, {Input.PunctualityAttendance}, {Input.ProjectExecution}, {Input.OrderCleanliness}, {Input.TechnicalSkills},
    {variablePercent}, {notes}, {adminId}, {now}, {now}, {now}
)
ON CONFLICT (""UserId"", ""PeriodStart"", ""PeriodEnd"")
DO UPDATE SET
    ""PersonalPerformance"" = EXCLUDED.""PersonalPerformance"",
    ""Teamwork"" = EXCLUDED.""Teamwork"",
    ""ParticipationInTeam"" = EXCLUDED.""ParticipationInTeam"",
    ""PunctualityAttendance"" = EXCLUDED.""PunctualityAttendance"",
    ""ProjectExecution"" = EXCLUDED.""ProjectExecution"",
    ""OrderCleanliness"" = EXCLUDED.""OrderCleanliness"",
    ""TechnicalSkills"" = EXCLUDED.""TechnicalSkills"",
    ""VariablePercent"" = EXCLUDED.""VariablePercent"",
    ""Notes"" = EXCLUDED.""Notes"",
    ""RatedByUserId"" = EXCLUDED.""RatedByUserId"",
    ""RatedAt"" = EXCLUDED.""RatedAt"",
    ""UpdatedAt"" = EXCLUDED.""UpdatedAt"";
");

        return RedirectToPage("/Admin/Performance/Dashboard", new { year = ps.Year, month = ps.Month, half = (ps.Day <= 15 ? 1 : 2) });
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
