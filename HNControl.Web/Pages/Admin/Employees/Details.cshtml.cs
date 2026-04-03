using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public string UserId { get; set; } = default!;
    public EmployeeProfile? Profile { get; set; }
    public EmployeeProfile Employee => Profile!;  // ✅ para que compile Details.cshtml
    public EmployeeProfile? EmployeeOrNull => Profile;

    // ====== Mes seleccionado (soporta ?ym=YYYY-MM o ?month=YYYY-MM) ======
    [BindProperty(SupportsGet = true)]
    public string? ym { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? month { get; set; }

    public List<SelectListItem> MonthItems { get; set; } = new();

    public string SelectedMonthText { get; set; } = "";
    public string SelectedYm => SelectedMonthText;
    public string SelectedMonth => SelectedMonthText;

    public DateTime MonthStartUtc { get; set; }
    public DateTime MonthEndUtc { get; set; }

    // ====== KPI ======
    public decimal KPIAvgScore { get; set; }

    public string KpiLabelsJson { get; set; } = "[]";
    public string KpiValuesJson { get; set; } = "[]";

    public string KpiMonthLabelsJson => KpiLabelsJson;
    public string KpiMonthValuesJson => KpiValuesJson;

    // ====== Variable ======
    public decimal VariablePercentAvg { get; set; }
    public decimal VariableAvgPercent => VariablePercentAvg;

    public string VariableHistoryLabelsJson { get; set; } = "[]";
    public string VariableHistoryValuesJson { get; set; } = "[]";

    public string VarHistLabelsJson => VariableHistoryLabelsJson;
    public string VarHistValuesJson => VariableHistoryValuesJson;

    // ====== Viáticos ======
    public decimal WeeklyViaticTotal { get; set; }
    public decimal CurrentMonthViaticTotal => WeeklyViaticTotal;
    public decimal CurrentDeductionsTotal { get; set; }
    public decimal CurrentBonusesTotal { get; set; }
    public decimal CurrentSalesCommissionsTotal { get; set; }
    public decimal CurrentNetQuincenal { get; set; }
    public decimal CurrentVariablePercent { get; set; }
    public string CurrentPayrollPeriod { get; set; } = "";
    public int VacationRemaining { get; set; }
    public int ExamsPendingCount { get; set; }

    public List<WeekSummary> CurrentMonthWeeks { get; set; } = new();

    public record WeekSummary(
        Guid? WeekId,
        string Label,
        string Range,
        DateTime WeekStart,
        DateTime WeekEnd,
        decimal Total,
        decimal Billable,
        decimal NonBillable
    )
    {
        public string WeekLabel => Label;
    }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToPage("/Admin/Employees/Index");

        UserId = userId;

        Profile = await _db.EmployeeProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (Profile == null)
            return NotFound();

        var key = !string.IsNullOrWhiteSpace(ym) ? ym : month;
        (MonthStartUtc, MonthEndUtc, SelectedMonthText) = ParseMonthOrDefault(key);

        BuildMonthItems(SelectedMonthText);

        await LoadKpiMonthAsync(userId);
        await LoadKpiAvg90Async(userId);
        await LoadVariableHistoryAsync(userId);
        await LoadViaticWeeksAsync(userId);
        await LoadPayrollSnapshotAsync(userId);
        await LoadVacationsAndExamsAsync(userId);

        return Page();
    }

    private void BuildMonthItems(string selected)
    {
        var today = DateTime.UtcNow.Date;
        var first = new DateTime(today.Year, today.Month, 1);

        MonthItems = Enumerable.Range(0, 12)
            .Select(i =>
            {
                var dt = first.AddMonths(-i);
                var value = $"{dt:yyyy-MM}";
                var text = dt.ToString("MMMM yyyy");
                return new SelectListItem(text, value, value == selected);
            })
            .ToList();
    }

    private static (DateTime startUtc, DateTime endUtc, string textKey) ParseMonthOrDefault(string? key)
    {
        DateTime monthStart;
        string textKey;

        if (!string.IsNullOrWhiteSpace(key) &&
            DateTime.TryParse($"{key}-01", out var parsed))
        {
            monthStart = new DateTime(parsed.Year, parsed.Month, 1);
            textKey = $"{monthStart:yyyy-MM}";
        }
        else
        {
            var now = DateTime.UtcNow;
            monthStart = new DateTime(now.Year, now.Month, 1);
            textKey = $"{monthStart:yyyy-MM}";
        }

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var startUtc = TimeUtil.UtcDate(monthStart);
        var endUtc = TimeUtil.UtcDate(monthEnd);

        return (startUtc, endUtc, textKey);
    }

    private async Task LoadKpiMonthAsync(string employeeId)
    {
        var list = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == employeeId && r.PeriodStart >= MonthStartUtc && r.PeriodStart <= MonthEndUtc)
            .OrderBy(r => r.PeriodStart)
            .Select(r => new
            {
                Label = $"{r.PeriodStart:dd MMM}",
                Score = (decimal)(r.PersonalPerformance + r.Teamwork + r.ParticipationInTeam + r.PunctualityAttendance + r.ProjectExecution + r.OrderCleanliness + r.TechnicalSkills) / 7m
            })
            .ToListAsync();

        KpiLabelsJson = JsonSerializer.Serialize(list.Select(x => x.Label));
        KpiValuesJson = JsonSerializer.Serialize(list.Select(x => x.Score));
    }

    private async Task LoadKpiAvg90Async(string employeeId)
    {
        var since = TimeUtil.UtcDate(DateTime.UtcNow.Date.AddDays(-90));

        var scores = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == employeeId && r.CreatedAt >= since)
            .Select(r => (decimal)(r.PersonalPerformance + r.Teamwork + r.ParticipationInTeam + r.PunctualityAttendance + r.ProjectExecution + r.OrderCleanliness + r.TechnicalSkills) / 7m)
            .ToListAsync();

        KPIAvgScore = scores.Count == 0 ? 0m : Math.Round(scores.Average(), 2);
    }

    private async Task LoadVariableHistoryAsync(string employeeId)
    {
        var list = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == employeeId)
            .OrderByDescending(r => r.PeriodStart)
            .Take(12)
            .OrderBy(r => r.PeriodStart)
            .Select(r => new
            {
                Label = $"{r.PeriodStart:yyyy-MM}",
                Var = r.VariablePercent
            })
            .ToListAsync();

        VariableHistoryLabelsJson = JsonSerializer.Serialize(list.Select(x => x.Label));
        VariableHistoryValuesJson = JsonSerializer.Serialize(list.Select(x => x.Var));

        VariablePercentAvg = list.Count == 0 ? 0m : Math.Round(list.Average(x => x.Var), 4);
    }

    private async Task LoadViaticWeeksAsync(string employeeId)
    {
        var monthStartLocal = new DateTime(MonthStartUtc.Year, MonthStartUtc.Month, 1);
        var monthEndLocal = monthStartLocal.AddMonths(1).AddDays(-1);

        var min = StartOfWeek(monthStartLocal, DayOfWeek.Monday);
        var max = monthEndLocal;

        var minUtc = TimeUtil.UtcDate(min);
        var maxUtc = TimeUtil.UtcDate(max);

        var weeks = await _db.ViaticWeeks
            .AsNoTracking()
            .Where(w => w.UserId == employeeId && w.WeekStartDate >= minUtc && w.WeekStartDate <= maxUtc)
            .OrderBy(w => w.WeekStartDate)
            .ToListAsync();

        CurrentMonthWeeks = new();

        decimal total = 0m;

        foreach (var w in weeks)
        {
            var ws = w.WeekStartDate.Date;
            var we = ws.AddDays(6);

            var label = $"Semana {GetWeekOfMonth(ws)}";
            var range = $"{ws:yyyy-MM-dd} - {we:yyyy-MM-dd}";

            var sum = w.TotalAmount;

            var bill = w.BillableAmount;
            var nonBill = w.TotalAmount - w.BillableAmount;
            if (nonBill < 0) nonBill = 0;

            CurrentMonthWeeks.Add(new WeekSummary(
                w.Id,
                label,
                range,
                ws,
                we,
                sum,
                bill,
                nonBill
            ));

            total += sum;
        }

        WeeklyViaticTotal = total;
    }

    private static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
    {
        int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
        return dt.AddDays(-1 * diff).Date;
    }

    private static int GetWeekOfMonth(DateTime date)
    {
        var first = new DateTime(date.Year, date.Month, 1);
        var firstMonday = StartOfWeek(first, DayOfWeek.Monday);
        return (int)Math.Floor((date.Date - firstMonday).TotalDays / 7) + 1;
    }

    private async Task LoadPayrollSnapshotAsync(string userId)
    {
        var nowLocal = DateTime.Now.Date;
        var periodStart = nowLocal.Day <= 15
            ? TimeUtil.UtcDate(new DateTime(nowLocal.Year, nowLocal.Month, 1))
            : TimeUtil.UtcDate(new DateTime(nowLocal.Year, nowLocal.Month, 16));
        var periodEnd = nowLocal.Day <= 15
            ? TimeUtil.UtcDate(new DateTime(nowLocal.Year, nowLocal.Month, 15))
            : TimeUtil.UtcDate(new DateTime(nowLocal.Year, nowLocal.Month, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month)));

        var review = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PeriodStart)
            .ThenByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        var vp = review?.VariablePercent ?? 0m;
        if (vp < 0m) vp = 0m;
        if (vp > 1m) vp = 1m;
        CurrentVariablePercent = vp;

        var baseQ = Employee.SalaryBase / 2m;
        var total = Math.Round((baseQ * 0.80m) + (baseQ * 0.20m * vp), 2);
        var (ded, bon) = await CalcPayrollAdjustmentsAsync(userId, baseQ, total, periodStart, periodEnd);
        var commissions = await CalcSalesCommissionsAsync(userId, baseQ, total, periodStart, periodEnd);

        CurrentDeductionsTotal = ded;
        CurrentBonusesTotal = bon;
        CurrentSalesCommissionsTotal = commissions;
        CurrentNetQuincenal = Math.Max(0m, Math.Round(total - ded + bon, 2));
        CurrentPayrollPeriod = $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}";
    }

    private async Task LoadVacationsAndExamsAsync(string userId)
    {
        var year = DateTime.Now.Year;
        var allowance = Employee.HireDate.HasValue
            ? VacationPolicyMxLft.GetAnnualVacationDays(Employee.HireDate, DateTime.Now.Date)
            : Employee.VacationAllowanceDays;

        var used = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Approved
                        && x.StartDate.Year == year)
            .SumAsync(x => (int?)x.TotalDays) ?? 0;

        VacationRemaining = Math.Max(0, allowance - used);

        ExamsPendingCount = await _db.ExamAssignments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == ExamAssignmentStatus.Assigned || x.Status == ExamAssignmentStatus.InProgress)
            .CountAsync();
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
            var active = await _db.EmployeeDeductions
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Where(d => d.StartDate <= periodEnd && (d.EndDate == null || d.EndDate >= periodStart))
                .ToListAsync();

            var result = PayrollDeductionMath.CalculateTotals(active, baseQuincenal, estimatedQuincenal, periodStart, periodEnd);
            return (result.deductions, result.bonuses);
        }
        catch
        {
            return (0m, 0m);
        }
    }

    private async Task<decimal> CalcSalesCommissionsAsync(
        string userId,
        decimal baseQuincenal,
        decimal estimatedQuincenal,
        DateTime periodStart,
        DateTime periodEnd)
    {
        try
        {
            var active = await _db.EmployeeDeductions
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Where(d => d.Type == EmployeeDeductionType.ComisionVenta && d.Direction == EmployeeDeductionDirection.Bonus)
                .Where(d => d.StartDate <= periodEnd && (d.EndDate == null || d.EndDate >= periodStart))
                .ToListAsync();

            var result = PayrollDeductionMath.CalculateTotals(active, baseQuincenal, estimatedQuincenal, periodStart, periodEnd);
            return result.bonuses;
        }
        catch
        {
            return 0m;
        }
    }
}
