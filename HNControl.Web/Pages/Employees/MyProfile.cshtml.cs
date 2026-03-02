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

    public record PerfMini(string Period, decimal VariablePercent, decimal TotalQuincenal, decimal DeductionsQuincenal, decimal BonusesQuincenal, decimal NetQuincenal);
    public PerfMini? CurrentPay { get; set; }

    public record DeductionMini(
        string Concept,
        EmployeeDeductionType Type,
        EmployeeDeductionMode Mode,
        EmployeeDeductionDirection Direction,
        decimal PeriodAmount,
        decimal? RemainingAmount,
        DateTime StartDate,
        DateTime? EndDate
    );

    public List<DeductionMini> ActiveDeductions { get; set; } = new();
    public decimal DeductionsTotal { get; set; } = 0m;
    public decimal BonusesTotal { get; set; } = 0m;

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
        var net = Math.Round(total - DeductionsTotal + BonusesTotal, 2);
        if (net < 0m) net = 0m;

        var period = review == null
            ? $"{ps:yyyy-MM-dd} a {pe:yyyy-MM-dd}"
            : $"{review.PeriodStart:yyyy-MM-dd} a {review.PeriodEnd:yyyy-MM-dd}";

        CurrentPay = new PerfMini(period, vp, total, DeductionsTotal, BonusesTotal, net);
    }

    private async Task LoadDeductionsAsync(string userId, decimal baseQuincenal, decimal estimatedQuincenal)
    {
        ActiveDeductions = new();
        DeductionsTotal = 0m;
        BonusesTotal = 0m;

        try
        {
            var today = DateTime.UtcNow.Date;
            var now = DateTime.UtcNow;

            // ✅ Limpieza segura: si ya venció por fecha, lo marcamos como finalizado.
            await _db.EmployeeDeductions
                .Where(d => d.UserId == userId && d.IsActive && d.EndDate != null && d.EndDate < today)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsActive, false)
                    .SetProperty(p => p.UpdatedAt, now));

            var (periodStart, periodEnd, half) = GetPayPeriod(today);

            var deds = await _db.EmployeeDeductions
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Where(d => d.StartDate <= today && (d.EndDate == null || d.EndDate >= today))
                .OrderBy(d => d.Type)
                .ThenBy(d => d.Concept)
                .ToListAsync();

            var setEndIds = new List<Guid>();
            var expireIds = new List<Guid>();

            foreach (var d in deds)
            {
                // 1) Frecuencia / quincena aplicable
                if (!ShouldApplyInThisPeriod(d, half))
                    continue;

                // 2) Plazo (cuántos periodos ha "corrida" la deducción)
                var occ = CountOccurrencesUpToPeriodStart(d, d.StartDate, periodStart);
                if (occ <= 0) continue;

                if (d.TermCount.HasValue && d.TermCount.Value > 0 && occ > d.TermCount.Value)
                {
                    // Ya se pasó del plazo -> no aplica y lo damos por finalizado.
                    expireIds.Add(d.Id);
                    continue;
                }

                // 3) Cálculo por periodo (quincenal vs mensual)
                var factor = d.Frequency == EmployeeDeductionFrequency.Monthly ? 2m : 1m;

                var amount = d.Mode switch
                {
                    EmployeeDeductionMode.FixedAmount => d.Amount,
                    EmployeeDeductionMode.PercentOfBase => (baseQuincenal * factor) * d.Rate,
                    EmployeeDeductionMode.PercentOfEstimatedPay => (estimatedQuincenal * factor) * d.Rate,
                    _ => d.Amount
                };

                amount = Math.Round(amount, 2);
                if (amount < 0m) amount = 0m;

                // 4) Préstamos: cap al saldo estimado (si hay Total o Saldo capturado)
                decimal? remainingAfter = d.RemainingAmount;

                if (d.Type == EmployeeDeductionType.Prestamo)
                {
                    var principal = d.TotalAmount ?? d.RemainingAmount;

                    if (principal.HasValue && principal.Value > 0m && amount > 0m)
                    {
                        var paidBefore = amount * Math.Max(0, occ - 1);
                        var remBefore = principal.Value - paidBefore;
                        if (remBefore < 0m) remBefore = 0m;

                        if (amount > remBefore) amount = remBefore;

                        var remAfter = remBefore - amount;
                        if (remAfter < 0m) remAfter = 0m;

                        remainingAfter = Math.Round(remAfter, 2);

                        // Si se termina en este periodo, programamos EndDate al fin de la quincena (para que aplique hoy y se "cierre" después)
                        if (remAfter <= 0m && (d.EndDate == null || d.EndDate.Value > periodEnd))
                            setEndIds.Add(d.Id);
                    }
                    else if (d.RemainingAmount.HasValue)
                    {
                        // Compatibilidad con el comportamiento anterior
                        if (d.RemainingAmount.Value <= 0m) continue;
                        if (amount > d.RemainingAmount.Value) amount = d.RemainingAmount.Value;
                    }
                }

                if (amount <= 0m) continue;

                // Si es el último periodo del plazo, programamos fin al cierre de la quincena
                if (d.TermCount.HasValue && d.TermCount.Value > 0 && occ == d.TermCount.Value)
                {
                    if (d.EndDate == null || d.EndDate.Value > periodEnd)
                        setEndIds.Add(d.Id);
                }

                ActiveDeductions.Add(new DeductionMini(
                    d.Concept,
                    d.Type,
                    d.Mode,
                    d.Direction,
                    amount,
                    remainingAfter,
                    d.StartDate,
                    d.EndDate
                ));

                if (d.Direction == EmployeeDeductionDirection.Bonus)
                    BonusesTotal += amount;
                else
                    DeductionsTotal += amount;
            }

            if (setEndIds.Count > 0)
            {
                // Distinct para no repetir
                var ids = setEndIds.Distinct().ToList();

                await _db.EmployeeDeductions
                    .Where(x => x.UserId == userId && ids.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.EndDate, periodEnd)
                        .SetProperty(p => p.UpdatedAt, now));
            }

            if (expireIds.Count > 0)
            {
                var ids = expireIds.Distinct().ToList();
                var yesterday = today.AddDays(-1);

                await _db.EmployeeDeductions
                    .Where(x => x.UserId == userId && ids.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.IsActive, false)
                        .SetProperty(p => p.EndDate, yesterday)
                        .SetProperty(p => p.UpdatedAt, now));
            }

            DeductionsTotal = Math.Round(DeductionsTotal, 2);
            BonusesTotal = Math.Round(BonusesTotal, 2);
        }
        catch
        {
            // Tablas aún no existen o no accesibles: nos quedamos sin deducciones.
            ActiveDeductions = new();
            DeductionsTotal = 0m;
            BonusesTotal = 0m;
        }
    }

    private static (DateTime PeriodStart, DateTime PeriodEnd, int Half) GetPayPeriod(DateTime date)
    {
        var y = date.Year;
        var m = date.Month;

        if (date.Day <= 15)
        {
            return (new DateTime(y, m, 1), new DateTime(y, m, 15), 1);
        }

        var last = DateTime.DaysInMonth(y, m);
        return (new DateTime(y, m, 16), new DateTime(y, m, last), 2);
    }

    private static bool ShouldApplyInThisPeriod(EmployeeDeduction d, int currentHalf)
    {
        if (d.Frequency != EmployeeDeductionFrequency.Monthly)
            return true;

        var half = (d.ApplyOnHalf is 1 or 2) ? d.ApplyOnHalf.Value : 2;
        return currentHalf == half;
    }

    private static int CountOccurrencesUpToPeriodStart(EmployeeDeduction d, DateTime startDate, DateTime currentPeriodStart)
    {
        if (currentPeriodStart < startDate.Date) return 0;

        var first = d.Frequency switch
        {
            EmployeeDeductionFrequency.Monthly => GetMonthlyPeriodStart(startDate, (d.ApplyOnHalf is 1 or 2) ? d.ApplyOnHalf.Value : 2),
            _ => GetBiweeklyPeriodStart(startDate)
        };

        if (currentPeriodStart < first) return 0;

        var count = 1;
        var cursor = first;

        while (true)
        {
            cursor = d.Frequency switch
            {
                EmployeeDeductionFrequency.Monthly => cursor.AddMonths(1),
                _ => NextBiweeklyStart(cursor)
            };

            if (cursor > currentPeriodStart) break;
            count++;
        }

        return count;
    }

    private static DateTime GetBiweeklyPeriodStart(DateTime d)
        => d.Day <= 15 ? new DateTime(d.Year, d.Month, 1) : new DateTime(d.Year, d.Month, 16);

    private static DateTime GetMonthlyPeriodStart(DateTime d, int applyHalf)
    {
        if (applyHalf == 2)
            return new DateTime(d.Year, d.Month, 16);

        // applyHalf == 1
        return d.Day <= 15
            ? new DateTime(d.Year, d.Month, 1)
            : new DateTime(d.AddMonths(1).Year, d.AddMonths(1).Month, 1);
    }

    private static DateTime NextBiweeklyStart(DateTime periodStart)
        => periodStart.Day == 1
            ? new DateTime(periodStart.Year, periodStart.Month, 16)
            : new DateTime(periodStart.AddMonths(1).Year, periodStart.AddMonths(1).Month, 1);

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

        // ✅ Allowance automático por LFT (si no hay HireDate, usamos el valor manual)
        VacationAllowance = (Profile?.HireDate != null)
            ? VacationPolicyMxLft.GetAnnualVacationDays(Profile.HireDate, DateTime.Now.Date)
            : (Profile?.VacationAllowanceDays ?? 0);

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
