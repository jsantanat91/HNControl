using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Employees;

[Authorize(Policy = "EmployeeOnly")]
public class MyProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly ApplicationDbContext _db;

    public MyProfileModel(UserManager<ApplicationUser> userMgr, ApplicationDbContext db)
    {
        _userMgr = userMgr;
        _db = db;
    }

    public EmployeeProfile? Profile { get; set; }

    public record ViaticMini(Guid Id, DateTime WeekStart, ViaticWeekStatus Status, decimal Total, decimal Billable);
    public ViaticMini? CurrentWeek { get; set; }
    public List<ViaticMini> RecentWeeks { get; set; } = new();

    public record PerfMini(string Period, decimal VariablePercent, decimal TotalQuincenal);
    public PerfMini? CurrentPay { get; set; }

    public record Eval360Mini(Guid CampaignId, string Title, DateTime Start, DateTime End, decimal AutoPct, decimal OthersPct, int OthersCount);
    public Eval360Mini? LastEval360 { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return;

        Profile = await _db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (Profile == null) return;

        await LoadViaticosAsync(userId);
        await LoadPayrollAsync(userId);
        await LoadEval360Async(userId);
    }

    private async Task LoadViaticosAsync(string userId)
    {
        var monday = ToMonday(DateTime.UtcNow.Date);

        var weeks = await _db.ViaticWeeks
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.WeekStartDate)
            .Select(w => new ViaticMini(
                w.Id,
                w.WeekStartDate,
                w.Status,
                w.TotalAmount,
                w.BillableAmount
            ))
            .Take(8)
            .ToListAsync();

        CurrentWeek = weeks.FirstOrDefault(w => w.WeekStart.Date == monday) ?? weeks.FirstOrDefault();
        RecentWeeks = weeks.Take(5).ToList();
    }

    private async Task LoadPayrollAsync(string userId)
    {
        // Periodo actual (quincena)
        var now = DateTime.Now.Date;
        (DateTime ps, DateTime pe) = now.Day <= 15
            ? (TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 1)), TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 15)))
            : (TimeUtil.UtcDate(new DateTime(now.Year, now.Month, 16)), TimeUtil.UtcDate(new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month))));

        // Robusto contra timestamptz con hora
        var review = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId
                        && r.PeriodStart >= ps && r.PeriodStart < ps.AddDays(1)
                        && r.PeriodEnd >= pe && r.PeriodEnd < pe.AddDays(1))
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        // Si no hay review en la quincena, usamos la última
        if (review == null)
        {
            review = await _db.PerformanceReviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.PeriodStart)
                .ThenByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        var vp = review?.VariablePercent ?? 0m;
        if (vp < 0m) vp = 0m;
        if (vp > 1m) vp = 1m;

        var baseQ = Profile!.SalaryBase / 2m;
        var fijo80 = baseQ * 0.80m;
        var max20 = baseQ * 0.20m;
        var total = fijo80 + (max20 * vp);

        var period = review == null
            ? $"{ps:yyyy-MM-dd} a {pe:yyyy-MM-dd}"
            : $"{review.PeriodStart:yyyy-MM-dd} a {review.PeriodEnd:yyyy-MM-dd}";

        CurrentPay = new PerfMini(period, vp, total);
    }

    private async Task LoadEval360Async(string userId)
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var q = _db.Eval360Campaigns.AsNoTracking().Where(c => c.Status == Eval360CampaignStatus.Closed);
        if (!isAdmin) q = q.Where(c => c.ResultsVisibleToEmployee);

        var campaign = await q
            .OrderByDescending(c => c.PeriodEnd ?? c.CreatedAt)
            .FirstOrDefaultAsync();

        if (campaign == null) return;

        // Promedio global auto vs otros
        var answers = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.Assignment!.CampaignId == campaign.Id
                        && a.Assignment.SubjectUserId == userId
                        && a.Assignment.Status == Eval360AssignmentStatus.Submitted)
            .Select(a => new
            {
                a.Assignment!.IsSelf,
                a.Assignment.EvaluatorUserId,
                a.Score
            })
            .ToListAsync();

        if (!answers.Any()) return;

        var selfScores = answers.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
        var othScores = answers.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

        var autoAvg = selfScores.Any() ? selfScores.Average() : 0m;
        var othAvg = othScores.Any() ? othScores.Average() : 0m;

        var autoPct = Math.Round((autoAvg / 5m) * 100m, 0);
        var othPct = Math.Round((othAvg / 5m) * 100m, 0);

        var othersCount = answers.Where(x => !x.IsSelf)
            .Select(x => x.EvaluatorUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .Count();

        LastEval360 = new Eval360Mini(
            campaign.Id,
            campaign.Title,
            (campaign.PeriodStart ?? campaign.CreatedAt),
            (campaign.PeriodEnd ?? campaign.CreatedAt),
            autoPct,
            othPct,
            othersCount
        );
    }

    private static DateTime ToMonday(DateTime d)
    {
        var date = d.Date;
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }
}
