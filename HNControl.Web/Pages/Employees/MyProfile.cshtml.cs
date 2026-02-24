using System.Text.Json;
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

    public record PerfMini(string Period, decimal VariablePercent, decimal TotalQuincenal, decimal DeductionsQuincenal, decimal NetQuincenal);
    public PerfMini? CurrentPay { get; set; }

    public record DeductionMini(
        string Concept,
        EmployeeDeductionType Type,
        EmployeeDeductionMode Mode,
        decimal PeriodAmount,
        decimal? RemainingAmount,
        DateTime StartDate,
        DateTime? EndDate
    );

    public List<DeductionMini> ActiveDeductions { get; set; } = new();
    public decimal DeductionsTotal { get; set; } = 0m;

    public decimal NetQuincenal => CurrentPay?.NetQuincenal ?? 0m;

    public record Eval360Mini(Guid CampaignId, string Title, DateTime Start, DateTime End, decimal AutoPct, decimal OthersPct, int OthersCount, bool VisibleToEmployee);
    public Eval360Mini? LastEval360 { get; set; }

    public string SeniorityText { get; set; } = "";

    // Vacaciones
    public int VacYear { get; set; }
    public int VacationAllowance { get; set; }
    public int VacationUsed { get; set; }
    public int VacationRemaining { get; set; }
    public int VacationPending { get; set; }
    public DateTime? NextVacationStart { get; set; }
    public DateTime? NextVacationEnd { get; set; }

    // Exámenes
    public int ExamsAssignedCount { get; set; }
    public int ExamsInProgressCount { get; set; }
    public int ExamsSubmittedCount { get; set; }
    public int ExamsGradedCount { get; set; }

    public string ExamsLabelsJson { get; set; } = "[]";
    public string ExamsValuesJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return;

        Profile = await _db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (Profile == null) return;

        SeniorityText = CalcSeniority(Profile.HireDate);

        await LoadViaticosAsync(userId);
        await LoadPayrollAsync(userId);
        await LoadEval360Async(userId);
        await LoadLeavesAsync(userId);
        await LoadExamsAsync(userId);
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
        var total = Math.Round(fijo80 + (max20 * vp), 2);

        // Deducciones activas (si aún no existen tablas, no tronamos)
        await LoadDeductionsAsync(userId, baseQ, total);
        var net = Math.Round(total - DeductionsTotal, 2);
        if (net < 0m) net = 0m;

        var period = review == null
            ? $"{ps:yyyy-MM-dd} a {pe:yyyy-MM-dd}"
            : $"{review.PeriodStart:yyyy-MM-dd} a {review.PeriodEnd:yyyy-MM-dd}";

        CurrentPay = new PerfMini(period, vp, total, DeductionsTotal, net);
    }

    private async Task LoadDeductionsAsync(string userId, decimal baseQuincenal, decimal estimatedQuincenal)
    {
        ActiveDeductions = new();
        DeductionsTotal = 0m;

        try
        {
            var today = DateTime.UtcNow.Date;

            var deds = await _db.EmployeeDeductions
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Where(d => d.StartDate <= today && (d.EndDate == null || d.EndDate >= today))
                .OrderBy(d => d.Type)
                .ThenBy(d => d.Concept)
                .ToListAsync();

            foreach (var d in deds)
            {
                var amount = d.Mode switch
                {
                    EmployeeDeductionMode.FixedAmount => d.Amount,
                    EmployeeDeductionMode.PercentOfBase => baseQuincenal * d.Rate,
                    EmployeeDeductionMode.PercentOfEstimatedPay => estimatedQuincenal * d.Rate,
                    _ => d.Amount
                };

                amount = Math.Round(amount, 2);
                if (amount < 0m) amount = 0m;

                // Para préstamos con saldo, no descontamos más del saldo
                if (d.RemainingAmount.HasValue)
                {
                    if (d.RemainingAmount.Value <= 0m) continue;
                    if (amount > d.RemainingAmount.Value) amount = d.RemainingAmount.Value;
                }

                ActiveDeductions.Add(new DeductionMini(
                    d.Concept,
                    d.Type,
                    d.Mode,
                    amount,
                    d.RemainingAmount,
                    d.StartDate,
                    d.EndDate
                ));

                DeductionsTotal += amount;
            }

            DeductionsTotal = Math.Round(DeductionsTotal, 2);
        }
        catch
        {
            // Tablas aún no existen o no accesibles: nos quedamos sin deducciones.
            ActiveDeductions = new();
            DeductionsTotal = 0m;
        }
    }

    private async Task LoadEval360Async(string userId)
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);

        // ✅ Tomar la última campaña CERRADA donde este empleado sí tenga respuestas (si no, cae en "no hay resultados")
        var campaign = await _db.Eval360Campaigns
            .AsNoTracking()
            .Where(c => c.Status == Eval360CampaignStatus.Closed)
            .Where(c => _db.Eval360Assignments.Any(a => a.CampaignId == c.Id
                                                       && a.SubjectUserId == userId
                                                       && a.Status == Eval360AssignmentStatus.Submitted))
            .OrderByDescending(c => c.PeriodEnd ?? c.CreatedAt)
            .FirstOrDefaultAsync();

        if (campaign == null) return;

        var visibleToEmployee = isAdmin || campaign.ResultsVisibleToEmployee;

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
            othersCount,
            visibleToEmployee
        );
    }

    private async Task LoadLeavesAsync(string userId)
    {
        VacYear = DateTime.Now.Year;

        // Allowance viene del perfil
        VacationAllowance = Profile?.VacationAllowanceDays ?? 0;

        var used = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Approved
                        && x.StartDate.Year == VacYear)
            .SumAsync(x => (int?)x.TotalDays) ?? 0;

        VacationUsed = used;
        VacationRemaining = VacationAllowance - VacationUsed;
        if (VacationRemaining < 0) VacationRemaining = 0;

        VacationPending = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Pending)
            .CountAsync();

        var today = DateTime.UtcNow.Date;
        var next = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Approved
                        && x.StartDate >= today)
            .OrderBy(x => x.StartDate)
            .FirstOrDefaultAsync();

        NextVacationStart = next?.StartDate;
        NextVacationEnd = next?.EndDate;
    }

    private async Task LoadExamsAsync(string userId)
    {
        var items = await _db.ExamAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync();

        ExamsAssignedCount = items.Count(x => x.Status == ExamAssignmentStatus.Assigned);
        ExamsInProgressCount = items.Count(x => x.Status == ExamAssignmentStatus.InProgress);
        ExamsSubmittedCount = items.Count(x => x.Status == ExamAssignmentStatus.Submitted);
        ExamsGradedCount = items.Count(x => x.Status == ExamAssignmentStatus.Graded);

        var labels = new[] { "Assigned", "InProgress", "Submitted", "Graded" };
        var values = new[] { ExamsAssignedCount, ExamsInProgressCount, ExamsSubmittedCount, ExamsGradedCount };

        ExamsLabelsJson = JsonSerializer.Serialize(labels);
        ExamsValuesJson = JsonSerializer.Serialize(values);
    }

    private static string CalcSeniority(DateTime? hireDate)
    {
        if (!hireDate.HasValue) return "";

        var hd = hireDate.Value.Date;
        var now = DateTime.UtcNow.Date;

        var months = (now.Year - hd.Year) * 12 + (now.Month - hd.Month);
        if (now.Day < hd.Day) months -= 1;
        if (months < 0) months = 0;

        var years = months / 12;
        var rem = months % 12;
        return $"{years} año(s) {rem} mes(es)";
    }

    private static DateTime ToMonday(DateTime d)
    {
        var date = d.Date;
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }
}
