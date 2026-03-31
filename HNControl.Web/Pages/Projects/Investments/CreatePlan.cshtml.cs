using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Investments;

[Authorize(Roles = AppRoles.Admin)]
public class CreatePlanModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreatePlanModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid? InvestorId { get; set; }

    public List<SelectListItem> InvestorItems { get; set; } = new();
    public List<SelectListItem> ClientItems { get; set; } = new();

    public class InputModel
    {
        [Required]
        public Guid InvestorId { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [Range(1, 999999999)]
        public decimal PrincipalAmount { get; set; }

        [Range(0, 100)]
        public decimal ProfitPercentHuman { get; set; }

        [Range(1, 120)]
        public int PaymentCount { get; set; } = 12;

        public InvestmentPeriodicity Periodicity { get; set; } = InvestmentPeriodicity.Monthly;
        public DateTime StartDate { get; set; } = DateTime.Today;

        [MaxLength(1200)]
        public string Notes { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        await LoadListsAsync();
        if (InvestorId.HasValue && InvestorItems.Any(x => x.Value == InvestorId.Value.ToString()))
            Input.InvestorId = InvestorId.Value;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (!ModelState.IsValid) return Page();

        var investor = await _db.InvestmentInvestors
            .FirstOrDefaultAsync(x => x.Id == Input.InvestorId && x.IsActive);
        if (investor == null)
        {
            ModelState.AddModelError("", "Selecciona un inversionista valido.");
            return Page();
        }

        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == Input.ClientId);
        if (client == null)
        {
            ModelState.AddModelError("", "Selecciona un cliente valido.");
            return Page();
        }

        var now = DateTime.UtcNow;
        var profitPct = Math.Max(0m, Math.Min(1m, Input.ProfitPercentHuman / 100m));
        var totalProfit = Math.Round(Input.PrincipalAmount * profitPct, 2);
        var principalPer = Math.Round(Input.PrincipalAmount / Input.PaymentCount, 2);
        var profitPer = Math.Round(totalProfit / Input.PaymentCount, 2);

        var plan = new InvestmentPlan
        {
            InvestorId = Input.InvestorId,
            ClientId = Input.ClientId,
            Name = Input.Name.Trim(),
            PrincipalAmount = Math.Round(Input.PrincipalAmount, 2),
            ProfitPercent = profitPct,
            PaymentCount = Input.PaymentCount,
            Periodicity = Input.Periodicity,
            StartDate = Input.StartDate.Date,
            Notes = Input.Notes?.Trim() ?? "",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var due = Input.StartDate.Date;
        for (var i = 1; i <= Input.PaymentCount; i++)
        {
            var pp = i == Input.PaymentCount
                ? Math.Round(Input.PrincipalAmount - principalPer * (Input.PaymentCount - 1), 2)
                : principalPer;
            var gp = i == Input.PaymentCount
                ? Math.Round(totalProfit - profitPer * (Input.PaymentCount - 1), 2)
                : profitPer;

            plan.Payments.Add(new InvestmentPayment
            {
                PeriodNumber = i,
                DueDate = due,
                PrincipalPortion = pp,
                ProfitPortion = gp,
                TotalAmount = Math.Round(pp + gp, 2),
                IsPaid = false
            });
            due = AddPeriod(due, Input.Periodicity);
        }

        _db.InvestmentPlans.Add(plan);
        await _db.SaveChangesAsync();
        return RedirectToPage("/Projects/Investments/Details", new { id = Input.InvestorId });
    }

    private async Task LoadListsAsync()
    {
        InvestorItems = await _db.InvestmentInvestors
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName + " - " + x.Email, x.Id.ToString()))
            .ToListAsync();

        ClientItems = await _db.Clients
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }

    private static DateTime AddPeriod(DateTime date, InvestmentPeriodicity periodicity)
        => periodicity switch
        {
            InvestmentPeriodicity.Weekly => date.AddDays(7),
            InvestmentPeriodicity.Biweekly => date.AddDays(15),
            _ => date.AddMonths(1)
        };
}
