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
    public RateModel(ApplicationDbContext db) => _db = db;
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }

    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList MonthItems { get; set; } = default!;
    public SelectList HalfItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? EmployeeName { get; set; }

    public string? Error { get; set; }
    public string? Info { get; set; }

    public class InputModel
    {
        [Required] public string EmployeeId { get; set; } = "";
        [Required] public string Ym { get; set; } = ""; // yyyy-MM
        [Range(1, 2)] public int Half { get; set; } = 1;

        [Range(1, 5)] public int PersonalPerformance { get; set; } = 3;
        [Range(1, 5)] public int Teamwork { get; set; } = 3;
        [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
        [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
        [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
        [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

        [MaxLength(1200)] public string Notes { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string employeeId, string? ym = null, int? half = null, DateTime? start = null, DateTime? end = null)
    {
        await LoadListsAsync();

        Input.EmployeeId = employeeId;

        var (ps, pe, selectedYm, selectedHalf) = ResolvePeriod(ym, half, start, end);
        PeriodStart = ps;
        PeriodEnd = pe;
        Input.Ym = selectedYm;
        Input.Half = selectedHalf;

        MonthItems = BuildMonthItems(selectedYm);
        HalfItems = BuildHalfItems(selectedHalf);

        EmployeeName = await _db.EmployeeProfiles
            .Where(e => e.UserId == employeeId)
            .Select(e => e.FullName)
            .FirstOrDefaultAsync();

        var psUtc = TimeUtil.UtcDate(ps);
        var peUtc = TimeUtil.UtcDate(pe);

        var psMin = psUtc.AddDays(-1);
        var psMax = psUtc.AddDays(2);
        var peMin = peUtc.AddDays(-1);
        var peMax = peUtc.AddDays(2);

        var existing = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == employeeId
                     && r.PeriodStart >= psMin && r.PeriodStart < psMax
                     && r.PeriodEnd >= peMin && r.PeriodEnd < peMax)
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            Input.PersonalPerformance = existing.PersonalPerformance;
            Input.Teamwork = existing.Teamwork;
            Input.PunctualityAttendance = existing.PunctualityAttendance;
            Input.ProjectExecution = existing.ProjectExecution;
            Input.OrderCleanliness = existing.OrderCleanliness;
            Input.TechnicalSkills = existing.TechnicalSkills;
            Input.Notes = existing.Notes ?? "";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        MonthItems = BuildMonthItems(Input.Ym);
        HalfItems = BuildHalfItems(Input.Half);

        if (!ModelState.IsValid)
        {
            try
            {
                var (ps, pe) = ResolvePeriodFromUi(Input.Ym, Input.Half);
                PeriodStart = ps;
                PeriodEnd = pe;
            }
            catch
            {
                PeriodStart = DateTime.Today;
                PeriodEnd = DateTime.Today;
            }

            Error = "Revisa los campos. Hay datos inválidos.";
            return Page();
        }

        try
        {
            var (ps, pe) = ResolvePeriodFromUi(Input.Ym, Input.Half);
            PeriodStart = ps;
            PeriodEnd = pe;
            var psUtc = TimeUtil.UtcDate(ps);
            var peUtc = TimeUtil.UtcDate(pe);

            // Si existe evaluación "corrida", usamos su key exacta para actualizar y no insertar duplicado.
            var psMin = psUtc.AddDays(-1);
            var psMax = psUtc.AddDays(2);
            var peMin = peUtc.AddDays(-1);
            var peMax = peUtc.AddDays(2);

            var existing = await _db.PerformanceReviews
                .AsNoTracking()
                .Where(r => r.UserId == Input.EmployeeId
                         && r.PeriodStart >= psMin && r.PeriodStart < psMax
                         && r.PeriodEnd >= peMin && r.PeriodEnd < peMax)
                .OrderByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // 🔥 Npgsql (timestamptz) exige DateTimeKind.Utc. Si hay data vieja con Kind=Unspecified, la normalizamos.
                psUtc = TimeUtil.UtcDate(existing.PeriodStart);
                peUtc = TimeUtil.UtcDate(existing.PeriodEnd);
            }

            var notes = (Input.Notes ?? "").Trim();
            var sum = (decimal)(Input.PersonalPerformance + Input.Teamwork + Input.PunctualityAttendance +
                                Input.ProjectExecution + Input.OrderCleanliness + Input.TechnicalSkills);

            var variablePercent = Math.Round(sum / 30m, 4);
            variablePercent = Math.Clamp(variablePercent, 0m, 1m);

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var now = DateTime.UtcNow;

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""PerformanceReviews"" (
    ""Id"", ""UserId"", ""PeriodStart"", ""PeriodEnd"",
    ""PersonalPerformance"", ""Teamwork"", ""PunctualityAttendance"", ""ProjectExecution"", ""OrderCleanliness"", ""TechnicalSkills"",
    ""VariablePercent"", ""Notes"", ""RatedByUserId"", ""RatedAt"", ""CreatedAt"", ""UpdatedAt""
) VALUES (
    {Guid.NewGuid()}, {Input.EmployeeId}, {psUtc}, {peUtc},
    {Input.PersonalPerformance}, {Input.Teamwork}, {Input.PunctualityAttendance}, {Input.ProjectExecution}, {Input.OrderCleanliness}, {Input.TechnicalSkills},
    {variablePercent}, {notes}, {adminId}, {now}, {now}, {now}
)
ON CONFLICT (""UserId"", ""PeriodStart"", ""PeriodEnd"")
DO UPDATE SET
    ""PersonalPerformance"" = EXCLUDED.""PersonalPerformance"",
    ""Teamwork"" = EXCLUDED.""Teamwork"",
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

            Info = "✅ Evaluación guardada.";
        }
        catch (Exception ex)
        {
            try
            {
                var (ps, pe) = ResolvePeriodFromUi(Input.Ym, Input.Half);
                PeriodStart = ps;
                PeriodEnd = pe;
            }
            catch
            {
                PeriodStart = DateTime.Today;
                PeriodEnd = DateTime.Today;
            }

            Error = ex.Message;
        }

        return Page();
    }

    private async Task LoadListsAsync()
    {
        var employees = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "FullName");
    }

    private static SelectList BuildMonthItems(string selectedYm)
    {
        var now = DateTime.Today;
        var list = Enumerable.Range(0, 18)
            .Select(i => new DateTime(now.Year, now.Month, 1).AddMonths(-i))
            .Select(d => new { Id = d.ToString("yyyy-MM"), Name = d.ToString("MMMM yyyy") })
            .ToList();
        return new SelectList(list, "Id", "Name", selectedYm);
    }

    private static SelectList BuildHalfItems(int selected)
    {
        var list = new[]
        {
            new { Id = 1, Name = "1ª quincena (1–15)" },
            new { Id = 2, Name = "2ª quincena (16–fin)" }
        };
        return new SelectList(list, "Id", "Name", selected);
    }

    private static (DateTime ps, DateTime pe, string ym, int half) ResolvePeriod(string? ym, int? half, DateTime? start, DateTime? end)
    {
        if (!string.IsNullOrWhiteSpace(ym) && half is 1 or 2)
        {
            var (ps, pe) = ResolvePeriodFromUi(ym, half.Value);
            return (ps, pe, ym, half.Value);
        }

        if (start.HasValue && end.HasValue)
        {
            var s = start.Value.Date;
            var e = end.Value.Date;
            var ym2 = s.ToString("yyyy-MM");
            var h2 = s.Day <= 15 ? 1 : 2;
            return (s, e, ym2, h2);
        }

        var today = DateTime.Today;
        var defYm = new DateTime(today.Year, today.Month, 1).ToString("yyyy-MM");
        var defHalf = today.Day <= 15 ? 1 : 2;
        var (dps, dpe) = ResolvePeriodFromUi(defYm, defHalf);
        return (dps, dpe, defYm, defHalf);
    }

    private static (DateTime ps, DateTime pe) ResolvePeriodFromUi(string ym, int half)
    {
        var parts = ym.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m))
            throw new InvalidOperationException("Mes inválido.");

        var monthStart = new DateTime(y, m, 1);
        var lastDay = DateTime.DaysInMonth(y, m);

        if (half == 1)
            return (monthStart, new DateTime(y, m, Math.Min(15, lastDay)));

        var start = new DateTime(y, m, Math.Min(16, lastDay));
        return (start, new DateTime(y, m, lastDay));
    }
}