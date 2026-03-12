using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Controllers.Mobile;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/mobile/employee")]
public class EmployeeController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public EmployeeController(ApplicationDbContext db)
    {
        _db = db;
    }

    public record EmployeeProfileDto(
        string FullName,
        string Email,
        string Position,
        string Phone,
        string Nss,
        string Curp,
        string Address,
        decimal SalaryBase,
        DateTime? HireDate,
        DateTime? BirthDate,
        string SeniorityText);

    public record PayrollDto(
        string Period,
        decimal VariablePercent,
        decimal TotalQuincenal,
        decimal DeductionsQuincenal,
        decimal BonusesQuincenal,
        decimal NetQuincenal);

    public record PayrollHistoryPointDto(string Label, decimal VariablePercent, decimal NetQuincenal);

    public record DeductionDto(
        string Concept,
        string Direction,
        string Type,
        decimal PeriodAmount,
        decimal? TotalAmount,
        decimal? RemainingAmount,
        int? ProgressPaidPeriods,
        int? ProgressTotalPeriods);

    public record VacationsDto(
        int Year,
        int AllowanceDays,
        int UsedDays,
        int RemainingDays,
        int PendingRequests,
        DateTime? NextStart,
        DateTime? NextEnd);

    public record ExamsDto(int Assigned, int InProgress, int Submitted, int Graded);
    public record KpiMetricDto(string Name, decimal Score);
    public record KpiFeedbackDto(string Period, decimal VariablePercent, string Notes, string RatedBy, DateTime? RatedAt, List<KpiMetricDto> Metrics);
    public record Eval360CommentDto(string Competency, string Comment);
    public record Eval360FeedbackDto(
        string CampaignTitle,
        string Period,
        decimal AutoPercent,
        decimal OthersPercent,
        int OthersCount,
        bool VisibleToEmployee,
        List<Eval360CommentDto> Comments);

    public record ViaticWeekDto(Guid Id, DateTime WeekStart, string Status, decimal Total, decimal Billable);
    public record TicketHistoryPointDto(string Label, int Resolved);

    public record InventoryOrderDto(
        Guid AnchorId,
        DateTime RequestedAt,
        string Type,
        string ProjectTitle,
        string ResponsibleName,
        string StatusLabel,
        int LinesCount,
        string ItemsPreview);

    public record EmployeeDashboardDto(
        EmployeeProfileDto Profile,
        PayrollDto Payroll,
        List<PayrollHistoryPointDto> PayrollHistory,
        List<TicketHistoryPointDto> TicketHistory,
        KpiFeedbackDto? KpiFeedback,
        Eval360FeedbackDto? Eval360Feedback,
        List<DeductionDto> Deductions,
        VacationsDto Vacations,
        ExamsDto Exams,
        ViaticWeekDto? CurrentViaticWeek,
        List<InventoryOrderDto> InventoryOrders);

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(EmployeeDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var profile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            return NotFound(new { message = "Perfil no encontrado." });
        }

        var nowLocal = DateTime.Now.Date;
        var currentHalf = nowLocal.Day <= 15
            ? EmployeeDeductionApplyOnHalf.First
            : EmployeeDeductionApplyOnHalf.Second;
        var todayUtc = DateTime.UtcNow.Date;

        var periodStart = nowLocal.Day <= 15
            ? new DateTime(nowLocal.Year, nowLocal.Month, 1)
            : new DateTime(nowLocal.Year, nowLocal.Month, 16);
        var periodEnd = nowLocal.Day <= 15
            ? new DateTime(nowLocal.Year, nowLocal.Month, 15)
            : new DateTime(nowLocal.Year, nowLocal.Month, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month));

        var periodStartUtc = DateTime.SpecifyKind(periodStart, DateTimeKind.Utc);
        var periodEndUtc = DateTime.SpecifyKind(periodEnd, DateTimeKind.Utc);

        var latestReview = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.PeriodStart >= periodStartUtc && r.PeriodStart < periodStartUtc.AddDays(1)
                        && r.PeriodEnd >= periodEndUtc && r.PeriodEnd < periodEndUtc.AddDays(1))
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        if (latestReview == null)
        {
            latestReview = await _db.PerformanceReviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.PeriodStart)
                .ThenByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        var variablePercent = latestReview?.VariablePercent ?? 0m;
        if (variablePercent < 0m) variablePercent = 0m;
        if (variablePercent > 1m) variablePercent = 1m;
        var isAdmin = User.IsInRole(AppRoles.Admin);

        string ratedBy = "-";
        if (!string.IsNullOrWhiteSpace(latestReview?.RatedByUserId))
        {
            ratedBy = await _db.EmployeeProfiles
                .AsNoTracking()
                .Where(x => x.UserId == latestReview.RatedByUserId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync() ?? "-";

            if (ratedBy == "-")
            {
                ratedBy = await _db.Users
                    .AsNoTracking()
                    .Where(x => x.Id == latestReview.RatedByUserId)
                    .Select(x => x.Email ?? x.UserName ?? "-")
                    .FirstOrDefaultAsync() ?? "-";
            }
        }

        var baseQuincenal = profile.SalaryBase / 2m;
        var fixed80 = baseQuincenal * 0.80m;
        var max20 = baseQuincenal * 0.20m;
        var totalQuincenal = Math.Round(fixed80 + (max20 * variablePercent), 2);

        var activeDeductions = await _db.EmployeeDeductions
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .Where(d => d.StartDate <= todayUtc && (d.EndDate == null || d.EndDate >= todayUtc))
            .Where(d => d.Frequency == EmployeeDeductionFrequency.Biweekly
                        || (d.Frequency == EmployeeDeductionFrequency.Monthly
                            && (d.ApplyOnHalf == null || d.ApplyOnHalf == currentHalf)))
            .OrderBy(d => d.Type)
            .ThenBy(d => d.Concept)
            .ToListAsync();

        var deductions = new List<DeductionDto>();
        decimal deductionsTotal = 0m;
        decimal bonusesTotal = 0m;

        foreach (var d in activeDeductions)
        {
            var amount = d.Mode switch
            {
                EmployeeDeductionMode.FixedAmount => d.Amount,
                EmployeeDeductionMode.PercentOfBase => baseQuincenal * d.Rate,
                EmployeeDeductionMode.PercentOfEstimatedPay => totalQuincenal * d.Rate,
                _ => d.Amount
            };

            amount = Math.Round(amount, 2);
            if (amount < 0m) amount = 0m;

            if (d.RemainingAmount.HasValue)
            {
                if (d.RemainingAmount.Value <= 0m) continue;
                if (amount > d.RemainingAmount.Value) amount = d.RemainingAmount.Value;
            }

            if (d.Direction == EmployeeDeductionDirection.Bonus) bonusesTotal += amount;
            else deductionsTotal += amount;

            var totalPeriods = CalcTotalPeriods(d, amount);
            var paidPeriods = CalcPaidPeriods(d, amount);

            deductions.Add(new DeductionDto(
                d.Concept,
                d.Direction == EmployeeDeductionDirection.Bonus ? "Bono" : "Deduccion",
                d.Type.ToString(),
                amount,
                d.TotalAmount,
                d.RemainingAmount,
                paidPeriods,
                totalPeriods));
        }

        deductionsTotal = Math.Round(deductionsTotal, 2);
        bonusesTotal = Math.Round(bonusesTotal, 2);
        var netQuincenal = Math.Round(Math.Max(0m, totalQuincenal - deductionsTotal + bonusesTotal), 2);

        var historyRows = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PeriodStart)
            .Take(6)
            .ToListAsync();

        var history = historyRows
            .OrderBy(x => x.PeriodStart)
            .Select(x =>
            {
                var vp = x.VariablePercent;
                if (vp < 0m) vp = 0m;
                if (vp > 1m) vp = 1m;
                var net = Math.Round(fixed80 + (max20 * vp) - deductionsTotal + bonusesTotal, 2);
                if (net < 0m) net = 0m;
                return new PayrollHistoryPointDto(
                    $"{x.PeriodStart:MM/dd}",
                    Math.Round(vp * 100m, 2),
                    net);
            })
            .ToList();

        var year = DateTime.Now.Year;
        var allowance = profile.HireDate.HasValue
            ? VacationPolicyMxLft.GetAnnualVacationDays(profile.HireDate.Value, DateTime.Now.Date)
            : profile.VacationAllowanceDays;

        var used = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Approved
                        && x.StartDate.Year == year)
            .SumAsync(x => (int?)x.TotalDays) ?? 0;

        var pending = await _db.LeaveRequests
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId
                             && x.Type == LeaveRequestType.Vacation
                             && x.Status == LeaveRequestStatus.Pending);

        var nextVac = await _db.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Type == LeaveRequestType.Vacation
                        && x.Status == LeaveRequestStatus.Approved
                        && x.StartDate >= todayUtc)
            .OrderBy(x => x.StartDate)
            .FirstOrDefaultAsync();

        var examItems = await _db.ExamAssignments.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        var exams = new ExamsDto(
            examItems.Count(x => x.Status == ExamAssignmentStatus.Assigned),
            examItems.Count(x => x.Status == ExamAssignmentStatus.InProgress),
            examItems.Count(x => x.Status == ExamAssignmentStatus.Submitted),
            examItems.Count(x => x.Status == ExamAssignmentStatus.Graded));

        var monday = ToMonday(todayUtc);
        var viaticRows = await _db.ViaticWeeks
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.WeekStartDate)
            .Take(8)
            .Select(x => new ViaticWeekDto(x.Id, x.WeekStartDate, x.Status.ToString(), x.TotalAmount, x.BillableAmount))
            .ToListAsync();
        var currentViatic = viaticRows.FirstOrDefault(x => x.WeekStart.Date == monday) ?? viaticRows.FirstOrDefault();

        var invLines = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Where(m => m.RequestedByUserId == userId || m.ResponsibleUserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .Take(2000)
            .ToListAsync();

        string LineLabel(InventoryMovement m)
        {
            var itemName = m.Item?.Name ?? "-";
            var unit = m.Item?.Unit ?? "pza";
            return $"{itemName} ({m.Quantity} {unit})";
        }

        string StatusBadge(IEnumerable<InventoryMovement> g)
        {
            var statuses = g.Select(x => x.Status).Distinct().ToList();
            if (statuses.Count == 1)
            {
                return statuses[0] switch
                {
                    InventoryMovementStatus.Pending => "Pendiente",
                    InventoryMovementStatus.Approved => "Aprobado",
                    InventoryMovementStatus.Rejected => "Rechazado",
                    _ => "-"
                };
            }
            return g.Any(x => x.Status == InventoryMovementStatus.Pending) ? "Parcial pendiente" : "Parcial";
        }

        var invOrders = invLines
            .GroupBy(m => new { m.RequestedAt, m.RequestedByUserId, m.Type, m.ProjectId, m.ResponsibleUserId })
            .OrderByDescending(g => g.Key.RequestedAt)
            .Take(30)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.Id).First();
                var previewList = g.OrderByDescending(x => x.Quantity).Take(3).Select(LineLabel).ToList();
                var preview = string.Join(", ", previewList);
                if (g.Count() > 3) preview += $" y {g.Count() - 3} mas";
                return new InventoryOrderDto(
                    first.Id,
                    g.Key.RequestedAt,
                    g.Key.Type.ToString(),
                    first.Project?.Title ?? "-",
                    string.IsNullOrWhiteSpace(first.ResponsibleName) ? "-" : first.ResponsibleName,
                    StatusBadge(g),
                    g.Count(),
                    string.IsNullOrWhiteSpace(preview) ? "-" : preview);
            })
            .ToList();

        var firstTicketMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var ticketResolvedDates = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId && t.ResolvedAt != null && t.ResolvedAt >= firstTicketMonth)
            .Select(t => t.ResolvedAt!.Value)
            .ToListAsync();

        var ticketHistory = new List<TicketHistoryPointDto>();
        for (var i = 5; i >= 0; i--)
        {
            var month = todayUtc.AddMonths(-i);
            var count = ticketResolvedDates.Count(x => x.Year == month.Year && x.Month == month.Month);
            ticketHistory.Add(new TicketHistoryPointDto(month.ToString("MM/yy"), count));
        }

        KpiFeedbackDto? kpiFeedback = null;
        if (latestReview != null)
        {
            var periodText = $"{latestReview.PeriodStart:yyyy-MM-dd} a {latestReview.PeriodEnd:yyyy-MM-dd}";
            var metrics = new List<KpiMetricDto>
            {
                new("Actitud", latestReview.PersonalPerformance),
                new("Participacion en equipo", latestReview.ParticipationInTeam),
                new("Puntualidad", latestReview.PunctualityAttendance),
                new("Trabajo en equipo", latestReview.Teamwork),
                new("Ejecucion", latestReview.ProjectExecution),
                new("Orden y limpieza", latestReview.OrderCleanliness),
                new("Habilidad tecnica", latestReview.TechnicalSkills),
            };
            kpiFeedback = new KpiFeedbackDto(
                periodText,
                Math.Round(variablePercent * 100m, 2),
                string.IsNullOrWhiteSpace(latestReview.Notes) ? "-" : latestReview.Notes.Trim(),
                ratedBy,
                latestReview.RatedAt,
                metrics);
        }

        Eval360FeedbackDto? eval360Feedback = null;
        var evalCampaign = await _db.Eval360Campaigns
            .AsNoTracking()
            .Where(c => c.Status == Eval360CampaignStatus.Closed)
            .Where(c => _db.Eval360Assignments.Any(a => a.CampaignId == c.Id
                                                       && a.SubjectUserId == userId
                                                       && a.Status == Eval360AssignmentStatus.Submitted))
            .OrderByDescending(c => c.PeriodEnd ?? c.CreatedAt)
            .FirstOrDefaultAsync();

        if (evalCampaign != null)
        {
            var visibleToEmployee = isAdmin || evalCampaign.ResultsVisibleToEmployee;
            var evalPeriod = $"{(evalCampaign.PeriodStart ?? evalCampaign.CreatedAt):yyyy-MM-dd} a {(evalCampaign.PeriodEnd ?? evalCampaign.CreatedAt):yyyy-MM-dd}";

            decimal autoPct = 0m;
            decimal othersPct = 0m;
            int othersCount = 0;
            var evalComments = new List<Eval360CommentDto>();

            if (visibleToEmployee)
            {
                var answers = await _db.Eval360Answers
                    .AsNoTracking()
                    .Where(a => a.Assignment!.CampaignId == evalCampaign.Id
                                && a.Assignment.SubjectUserId == userId
                                && a.Assignment.Status == Eval360AssignmentStatus.Submitted)
                    .Select(a => new
                    {
                        a.Assignment!.IsSelf,
                        a.Assignment.EvaluatorUserId,
                        a.Score
                    })
                    .ToListAsync();

                if (answers.Any())
                {
                    var selfScores = answers.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
                    var othScores = answers.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();
                    var autoAvg = selfScores.Any() ? selfScores.Average() : 0m;
                    var othAvg = othScores.Any() ? othScores.Average() : 0m;

                    autoPct = Math.Round((autoAvg / 5m) * 100m, 0);
                    othersPct = Math.Round((othAvg / 5m) * 100m, 0);
                    othersCount = answers.Where(x => !x.IsSelf)
                        .Select(x => x.EvaluatorUserId)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .Count();
                }

                evalComments = await _db.Eval360Comments
                    .AsNoTracking()
                    .Where(c => c.Assignment!.CampaignId == evalCampaign.Id
                                && c.Assignment.SubjectUserId == userId
                                && !c.Assignment.IsSelf
                                && c.Assignment.Status == Eval360AssignmentStatus.Submitted
                                && !string.IsNullOrWhiteSpace(c.CommentText))
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new Eval360CommentDto(
                        c.Competency != null ? c.Competency.Name : "Competencia",
                        c.CommentText.Trim()))
                    .Take(8)
                    .ToListAsync();
            }

            eval360Feedback = new Eval360FeedbackDto(
                evalCampaign.Title,
                evalPeriod,
                autoPct,
                othersPct,
                othersCount,
                visibleToEmployee,
                evalComments);
        }

        var dto = new EmployeeDashboardDto(
            new EmployeeProfileDto(
                profile.FullName,
                profile.Email,
                profile.Position,
                profile.Phone,
                profile.Nss,
                profile.Curp,
                profile.Address,
                profile.SalaryBase,
                profile.HireDate,
                profile.BirthDate,
                CalcSeniority(profile.HireDate)),
            new PayrollDto(
                latestReview == null ? $"{periodStart:yyyy-MM-dd} a {periodEnd:yyyy-MM-dd}" : $"{latestReview.PeriodStart:yyyy-MM-dd} a {latestReview.PeriodEnd:yyyy-MM-dd}",
                Math.Round(variablePercent * 100m, 2),
                totalQuincenal,
                deductionsTotal,
                bonusesTotal,
                netQuincenal),
            history,
            ticketHistory,
            kpiFeedback,
            eval360Feedback,
            deductions,
            new VacationsDto(
                year,
                allowance,
                used,
                Math.Max(0, allowance - used),
                pending,
                nextVac?.StartDate,
                nextVac?.EndDate),
            exams,
            currentViatic,
            invOrders);

        return Ok(dto);
    }

    private static DateTime ToMonday(DateTime date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }

    private static string CalcSeniority(DateTime? hireDate)
    {
        if (!hireDate.HasValue) return "-";
        var hd = hireDate.Value.Date;
        var now = DateTime.UtcNow.Date;
        var months = (now.Year - hd.Year) * 12 + (now.Month - hd.Month);
        if (now.Day < hd.Day) months -= 1;
        if (months < 0) months = 0;
        var years = months / 12;
        var rem = months % 12;
        return $"{years} anio(s) {rem} mes(es)";
    }

    private static int? CalcTotalPeriods(EmployeeDeduction d, decimal periodAmount)
    {
        if (d.TermCount.HasValue && d.TermCount.Value > 0) return d.TermCount.Value;
        if (d.TotalAmount.HasValue && d.TotalAmount.Value > 0m && periodAmount > 0m)
            return (int)Math.Ceiling(d.TotalAmount.Value / periodAmount);
        return null;
    }

    private static int? CalcPaidPeriods(EmployeeDeduction d, decimal periodAmount)
    {
        var total = CalcTotalPeriods(d, periodAmount);
        if (!total.HasValue || total.Value <= 0) return null;

        if (d.RemainingAmount.HasValue && d.TotalAmount.HasValue && d.TotalAmount.Value > 0m && periodAmount > 0m)
        {
            var remainingPeriods = (int)Math.Ceiling(Math.Max(0m, d.RemainingAmount.Value) / periodAmount);
            var paid = total.Value - remainingPeriods;
            if (paid < 0) paid = 0;
            if (paid > total.Value) paid = total.Value;
            return paid;
        }

        return null;
    }
}
