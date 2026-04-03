using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Resellers;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record EmployeeOption(string UserId, string FullName, string Email, string Phone);
    public record PartnerRow(Guid Id, string Name, string Email, ResellerPartyType Type, int ActivePlans, decimal PendingAmount);
    public record CommissionRow(Guid Id, string PartnerName, string ClientName, string Description, decimal CommissionAmount, int TotalPeriods, int PaidPeriods, DateTime StartDate, bool IsActive);
    public record PaymentRow(
        Guid PaymentId,
        string PartnerName,
        string Description,
        int PeriodNumber,
        DateTime DueDate,
        decimal Amount,
        bool IsPaid,
        DateTime? PaidAt,
        string DueBadgeText,
        string DueBadgeCss);

    public List<EmployeeOption> EmployeeOptions { get; set; } = new();
    public List<PartnerRow> Partners { get; set; } = new();
    public List<CommissionRow> Commissions { get; set; } = new();
    public List<PaymentRow> Payments { get; set; } = new();

    [BindProperty]
    public PartnerInput InputPartner { get; set; } = new();

    [BindProperty]
    public CommissionInput InputCommission { get; set; } = new();

    public List<SelectListItem> EmployeeItems { get; set; } = new();
    public List<SelectListItem> PartnerItems { get; set; } = new();
    public List<SelectListItem> ClientItems { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    public class PartnerInput
    {
        public ResellerPartyType PartyType { get; set; } = ResellerPartyType.External;
        public string? EmployeeUserId { get; set; }

        [MaxLength(200)]
        public string FullName { get; set; } = "";

        [EmailAddress, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(40)]
        public string Phone { get; set; } = "";

        [MaxLength(1200)]
        public string Notes { get; set; } = "";
    }

    public class CommissionInput
    {
        [Required]
        public Guid PartnerId { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        [Required, MaxLength(220)]
        public string Description { get; set; } = "";

        [Range(0, 99999999)]
        public decimal BaseAmount { get; set; }

        [Range(0, 100)]
        public decimal CommissionPercentHuman { get; set; } = 10m;

        [Range(1, 120)]
        public int PeriodCount { get; set; } = 1;

        public ResellerCommissionPeriodicity Periodicity { get; set; } = ResellerCommissionPeriodicity.OneTime;
        public DateTime StartDate { get; set; } = DateTime.Today;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreatePartnerAsync()
    {
        await LoadAsync();

        var now = DateTime.UtcNow;
        if (InputPartner.PartyType == ResellerPartyType.Employee)
        {
            if (string.IsNullOrWhiteSpace(InputPartner.EmployeeUserId))
            {
                ModelState.AddModelError("", "Selecciona un empleado.");
                return Page();
            }

            var emp = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == InputPartner.EmployeeUserId);
            if (emp == null)
            {
                ModelState.AddModelError("", "Empleado no encontrado.");
                return Page();
            }

            _db.ResellerPartners.Add(new ResellerPartner
            {
                PartyType = ResellerPartyType.Employee,
                EmployeeUserId = emp.UserId,
                FullName = emp.FullName,
                Email = emp.Email ?? "",
                Phone = emp.Phone ?? "",
                Notes = InputPartner.Notes?.Trim() ?? "",
                IsActive = true,
                CreatedAt = now
            });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(InputPartner.FullName))
            {
                ModelState.AddModelError("", "Nombre del reseller externo es obligatorio.");
                return Page();
            }

            _db.ResellerPartners.Add(new ResellerPartner
            {
                PartyType = ResellerPartyType.External,
                FullName = InputPartner.FullName.Trim(),
                Email = InputPartner.Email?.Trim() ?? "",
                Phone = InputPartner.Phone?.Trim() ?? "",
                Notes = InputPartner.Notes?.Trim() ?? "",
                IsActive = true,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync();
        Flash = "Reseller guardado correctamente.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateCommissionAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();

        var partner = await _db.ResellerPartners.FirstOrDefaultAsync(x => x.Id == InputCommission.PartnerId && x.IsActive);
        if (partner == null)
        {
            ModelState.AddModelError("", "Selecciona reseller valido.");
            return Page();
        }

        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == InputCommission.ClientId);
        if (client == null)
        {
            ModelState.AddModelError("", "Selecciona cliente valido.");
            return Page();
        }

        var baseAmount = InputCommission.BaseAmount;
        if (baseAmount <= 0m)
        {
            ModelState.AddModelError("", "Captura monto base para calcular comision.");
            return Page();
        }

        var pct = Math.Max(0m, Math.Min(1m, InputCommission.CommissionPercentHuman / 100m));
        var periodicity = InputCommission.Periodicity;
        var periodCount = periodicity == ResellerCommissionPeriodicity.OneTime ? 1 : Math.Max(1, InputCommission.PeriodCount);
        var amount = Math.Round(baseAmount * pct, 2);

        var plan = new ResellerCommissionPlan
        {
            PartnerId = partner.Id,
            ClientId = client.Id,
            SourceType = ResellerSourceType.ServiceOrder,
            ServiceOrderId = null,
            QuoteRequestId = null,
            Description = InputCommission.Description.Trim(),
            BaseAmount = Math.Round(baseAmount, 2),
            CommissionPercent = pct,
            CommissionAmount = amount,
            PeriodCount = periodCount,
            Periodicity = periodicity,
            StartDate = InputCommission.StartDate.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var due = plan.StartDate;
        for (var i = 1; i <= periodCount; i++)
        {
            plan.Payments.Add(new ResellerCommissionPayment
            {
                PeriodNumber = i,
                DueDate = due,
                Amount = amount,
                IsPaid = false
            });
            due = AddPeriod(due, periodicity);
        }

        _db.ResellerCommissionPlans.Add(plan);
        await _db.SaveChangesAsync();
        Flash = "Plan de comision creado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(Guid paymentId)
    {
        var payment = await _db.ResellerCommissionPayments
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == paymentId);
        if (payment == null) return NotFound();

        payment.IsPaid = true;
        payment.PaidAt = DateTime.UtcNow;
        if (payment.Plan != null)
        {
            var pending = await _db.ResellerCommissionPayments
                .Where(x => x.PlanId == payment.PlanId && !x.IsPaid && x.Id != payment.Id)
                .CountAsync();
            if (pending == 0) payment.Plan.IsActive = false;
        }

        await _db.SaveChangesAsync();
        Flash = "Pago de comision marcado correctamente.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        EmployeeOptions = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new EmployeeOption(x.UserId, x.FullName, x.Email ?? "", x.Phone ?? ""))
            .ToListAsync();

        EmployeeItems = EmployeeOptions
            .Select(x => new SelectListItem(x.FullName, x.UserId))
            .ToList();

        PartnerItems = await _db.ResellerPartners
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();

        ClientItems = await _db.Clients
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();

        var partners = await _db.ResellerPartners
            .AsNoTracking()
            .Include(x => x.CommissionPlans)
            .ThenInclude(x => x.Payments)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        Partners = partners.Select(p =>
        {
            var activePlans = p.CommissionPlans.Where(x => x.IsActive).ToList();
            var pending = activePlans.SelectMany(x => x.Payments).Where(x => !x.IsPaid).Sum(x => x.Amount);
            return new PartnerRow(p.Id, p.FullName, p.Email, p.PartyType, activePlans.Count, pending);
        }).ToList();

        var plans = await _db.ResellerCommissionPlans
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Client)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        Commissions = plans.Select(p => new CommissionRow(
            p.Id,
            p.Partner?.FullName ?? "-",
            p.Client?.Name ?? "-",
            p.Description,
            p.CommissionAmount,
            p.PeriodCount,
            p.Payments.Count(x => x.IsPaid),
            p.StartDate,
            p.IsActive
        )).ToList();

        var today = DateTime.Today;
        Payments = plans
            .SelectMany(p => p.Payments.Select(pay =>
            {
                var badge = ResolveDueBadge(pay.DueDate, pay.IsPaid, today);
                var concept = (p.Client?.Name ?? "-") + " - " + p.Description;
                return new PaymentRow(
                    pay.Id,
                    p.Partner?.FullName ?? "-",
                    concept,
                    pay.PeriodNumber,
                    pay.DueDate,
                    pay.Amount,
                    pay.IsPaid,
                    pay.PaidAt,
                    badge.text,
                    badge.css);
            }))
            .OrderBy(x => x.IsPaid)
            .ThenBy(x => x.DueDate)
            .Take(300)
            .ToList();
    }

    private static (string text, string css) ResolveDueBadge(DateTime dueDate, bool isPaid, DateTime today)
    {
        if (isPaid) return ("Pagado", "bg-success");
        var due = dueDate.Date;
        if (due < today) return ("Atrasado", "bg-danger");
        if (due == today) return ("Hoy", "bg-warning text-dark");
        if (due <= today.AddDays(3)) return ("Proximo", "bg-info text-dark");
        return ("Programado", "bg-secondary");
    }

    private static DateTime AddPeriod(DateTime date, ResellerCommissionPeriodicity periodicity)
        => periodicity switch
        {
            ResellerCommissionPeriodicity.Weekly => date.AddDays(7),
            ResellerCommissionPeriodicity.Biweekly => date.AddDays(15),
            ResellerCommissionPeriodicity.Monthly => date.AddMonths(1),
            _ => date
        };
}

