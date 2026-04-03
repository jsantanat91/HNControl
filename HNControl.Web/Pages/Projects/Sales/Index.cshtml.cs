using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Sales;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    [BindProperty] public string EmployeeUserId { get; set; } = "";
    [BindProperty] public decimal DefaultCommissionPercent { get; set; } = 0.05m;

    [BindProperty] public Guid QuoteId { get; set; }
    [BindProperty] public Guid SellerProfileId { get; set; }
    [BindProperty] public decimal CommissionPercent { get; set; } = 0.05m;
    [BindProperty] public string OpportunityNotes { get; set; } = "";

    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList SellerItems { get; set; } = default!;
    public SelectList QuoteItems { get; set; } = default!;

    public record SellerRow(Guid Id, string EmployeeName, string Email, decimal DefaultCommissionPercent, bool IsActive);
    public List<SellerRow> Sellers { get; set; } = new();

    public record OpportunityRow(
        Guid Id,
        string Folio,
        string Customer,
        string Seller,
        string Status,
        decimal Total,
        decimal CommissionPercent,
        decimal CommissionAmount,
        DateTime CreatedAt,
        bool ClientTemporaryLead,
        Guid? ClientId,
        Guid QuoteRequestId,
        Guid? BonusDeductionId);
    public List<OpportunityRow> Opportunities { get; set; } = new();

    public async Task OnGetAsync(Guid? quoteId = null)
    {
        if (quoteId.HasValue) QuoteId = quoteId.Value;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddSellerAsync()
    {
        if (string.IsNullOrWhiteSpace(EmployeeUserId))
        {
            Flash = "Selecciona empleado.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var exists = await _db.SalesSellerProfiles.AnyAsync(x => x.EmployeeUserId == EmployeeUserId);
        if (exists)
        {
            Flash = "Ese empleado ya esta dado de alta como vendedor.";
            FlashType = "warning";
            return RedirectToPage();
        }

        _db.SalesSellerProfiles.Add(new SalesSellerProfile
        {
            EmployeeUserId = EmployeeUserId,
            DefaultCommissionPercent = Math.Clamp(DefaultCommissionPercent, 0m, 1m),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var user = await _userMgr.FindByIdAsync(EmployeeUserId);
        if (user != null && !await _userMgr.IsInRoleAsync(user, AppRoles.Seller))
            await _userMgr.AddToRoleAsync(user, AppRoles.Seller);

        Flash = "Vendedor agregado.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateOpportunityAsync()
    {
        var quote = await _db.QuoteRequests.FirstOrDefaultAsync(x => x.Id == QuoteId);
        if (quote == null) return RedirectToPage();

        var existing = await _db.SalesOpportunities.FirstOrDefaultAsync(x => x.QuoteRequestId == QuoteId);
        if (existing != null)
        {
            Flash = "Esa cotizacion ya tiene oportunidad de venta.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var pct = Math.Clamp(CommissionPercent, 0m, 1m);
        var amount = Math.Round((quote.EstimatedTotal ?? quote.SubtotalAuto) * pct, 2);

        _db.SalesOpportunities.Add(new SalesOpportunity
        {
            QuoteRequestId = QuoteId,
            SellerProfileId = SellerProfileId,
            ClientId = quote.ClientId,
            Status = SalesOpportunityStatus.Prospect,
            CommissionPercent = pct,
            CommissionAmount = amount,
            Notes = (OpportunityNotes ?? "").Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Flash = "Oportunidad creada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloseWonAsync(Guid id)
    {
        var opp = await _db.SalesOpportunities
            .Include(x => x.QuoteRequest)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (opp == null) return RedirectToPage();

        opp.Status = SalesOpportunityStatus.ClosedWon;
        opp.ClosedAt = DateTime.UtcNow;
        opp.UpdatedAt = DateTime.UtcNow;

        if (opp.Client != null && opp.Client.IsTemporaryLead)
        {
            opp.Client.IsTemporaryLead = false;
            opp.Client.IsActive = true;
            opp.Client.ConvertedToFormalAt = DateTime.UtcNow;
            opp.Client.ClientCode = await NextFormalClientCodeAsync();
        }

        if (opp.QuoteRequest != null)
        {
            opp.QuoteRequest.Status = QuoteRequestStatus.Accepted;
            opp.QuoteRequest.AcceptedAt = DateTime.UtcNow;
            opp.QuoteRequest.AcceptedByUserId = User.Identity?.Name;
        }

        await _db.SaveChangesAsync();
        Flash = "Venta marcada como cerrada (won).";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkContractSignedAsync(Guid id)
    {
        var opp = await _db.SalesOpportunities
            .Include(x => x.SellerProfile!)
            .ThenInclude(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (opp == null) return RedirectToPage();
        if (opp.SellerProfile == null || opp.SellerProfile.Employee == null) return RedirectToPage();

        opp.Status = SalesOpportunityStatus.ContractSigned;
        opp.ContractSignedAt = DateTime.UtcNow;
        opp.UpdatedAt = DateTime.UtcNow;

        if (!opp.BonusDeductionId.HasValue)
        {
            var (startDate, endDate) = NextBiweeklyPeriod(DateTime.Today);
            var deduction = new EmployeeDeduction
            {
                UserId = opp.SellerProfile.EmployeeUserId,
                Type = EmployeeDeductionType.ComisionVenta,
                Direction = EmployeeDeductionDirection.Bonus,
                Concept = $"Comision de venta {opp.QuoteRequestId:N}",
                Mode = EmployeeDeductionMode.FixedAmount,
                Frequency = EmployeeDeductionFrequency.Biweekly,
                Amount = opp.CommissionAmount,
                Rate = 0m,
                StartDate = startDate,
                EndDate = endDate,
                TermCount = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.EmployeeDeductions.Add(deduction);
            await _db.SaveChangesAsync();
            opp.BonusDeductionId = deduction.Id;
        }

        opp.Status = SalesOpportunityStatus.CommissionApplied;
        await _db.SaveChangesAsync();
        Flash = "Contrato firmado y comision aplicada a proxima nomina.";
        FlashType = "success";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new { x.UserId, Label = x.FullName + " · " + x.Email })
            .ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "Label");

        Sellers = await _db.SalesSellerProfiles
            .AsNoTracking()
            .Include(x => x.Employee)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SellerRow(
                x.Id,
                x.Employee != null ? x.Employee.FullName : x.EmployeeUserId,
                x.Employee != null ? x.Employee.Email : "-",
                x.DefaultCommissionPercent,
                x.IsActive
            ))
            .ToListAsync();
        SellerItems = new SelectList(Sellers, "Id", "EmployeeName");

        var quotes = await _db.QuoteRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new { x.Id, Label = x.Folio + " · " + x.CustomerName })
            .ToListAsync();
        QuoteItems = new SelectList(quotes, "Id", "Label");

        Opportunities = await _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Include(x => x.SellerProfile!).ThenInclude(x => x.Employee)
            .Include(x => x.Client)
            .OrderByDescending(x => x.CreatedAt)
            .Take(400)
            .Select(x => new OpportunityRow(
                x.Id,
                x.QuoteRequest != null ? x.QuoteRequest.Folio : "-",
                x.QuoteRequest != null ? x.QuoteRequest.CustomerName : "-",
                x.SellerProfile != null && x.SellerProfile.Employee != null ? x.SellerProfile.Employee.FullName : "-",
                x.Status.ToString(),
                x.QuoteRequest != null ? (x.QuoteRequest.EstimatedTotal ?? x.QuoteRequest.SubtotalAuto) : 0m,
                x.CommissionPercent,
                x.CommissionAmount,
                x.CreatedAt,
                x.Client != null && x.Client.IsTemporaryLead,
                x.ClientId,
                x.QuoteRequestId,
                x.BonusDeductionId
            ))
            .ToListAsync();
    }

    private static (DateTime Start, DateTime End) NextBiweeklyPeriod(DateTime today)
    {
        if (today.Day <= 15)
        {
            var start = new DateTime(today.Year, today.Month, 16);
            var end = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            return (start, end);
        }

        var next = today.AddMonths(1);
        return (new DateTime(next.Year, next.Month, 1), new DateTime(next.Year, next.Month, 15));
    }

    private async Task<string> NextFormalClientCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => !c.IsTemporaryLead && !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-") && !c.ClientCode.StartsWith("HN-VENTA-"))
            .Select(c => c.ClientCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            if (int.TryParse(code.AsSpan(3), out var n) && n > max)
                max = n;
        }
        return $"HN-{max + 1:0000}";
    }
}
