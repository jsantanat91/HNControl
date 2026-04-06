using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IEmailSender _email;
    private readonly IEventEmailTemplateService _templates;
    private readonly IActionAccessService _actions;

    public IndexModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userMgr,
        IEmailSender email,
        IEventEmailTemplateService templates,
        IActionAccessService actions)
    {
        _db = db;
        _userMgr = userMgr;
        _email = email;
        _templates = templates;
        _actions = actions;
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
    public bool CanViewAll { get; set; }
    public bool CanManage { get; set; }
    public bool CanAssign { get; set; }
    public Guid? CurrentSellerProfileId { get; set; }
    public string CurrentSellerName { get; set; } = "";

    public async Task OnGetAsync(Guid? quoteId = null)
    {
        await EnsurePermissionsAsync();
        if (quoteId.HasValue) QuoteId = quoteId.Value;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddSellerAsync()
    {
        await EnsurePermissionsAsync();
        if (!CanManage)
        {
            Flash = "No tienes permiso para alta de vendedores.";
            FlashType = "warning";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(EmployeeUserId))
        {
            Flash = "Selecciona empleado.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var exists = await _db.SalesSellerProfiles.AnyAsync(x => x.EmployeeUserId == EmployeeUserId);
        if (exists)
        {
            Flash = "Ese empleado ya está dado de alta como vendedor.";
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
        await EnsurePermissionsAsync();
        if (!CanManage)
        {
            Flash = "No tienes permiso para crear oportunidades.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var quote = await _db.QuoteRequests.FirstOrDefaultAsync(x => x.Id == QuoteId);
        if (quote == null) return RedirectToPage();
        if (quote.Status == QuoteRequestStatus.Rejected)
        {
            Flash = "Solo se permiten cotizaciones activas (no rechazadas).";
            FlashType = "warning";
            return RedirectToPage();
        }

        var currentUserId = _userMgr.GetUserId(User) ?? string.Empty;
        Guid? resolvedSellerProfileId = SellerProfileId;
        if (!CanViewAll)
        {
            resolvedSellerProfileId = await _db.SalesSellerProfiles
                .Where(x => x.IsActive && x.EmployeeUserId == currentUserId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
            if (!resolvedSellerProfileId.HasValue)
            {
                Flash = "Tu usuario no tiene perfil de vendedor activo.";
                FlashType = "warning";
                return RedirectToPage();
            }
        }
        else if (!resolvedSellerProfileId.HasValue || resolvedSellerProfileId == Guid.Empty)
        {
            Flash = "Selecciona vendedor para crear la oportunidad.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var existing = await _db.SalesOpportunities.FirstOrDefaultAsync(x => x.QuoteRequestId == QuoteId);
        if (existing != null)
        {
            Flash = "Esa cotización ya tiene oportunidad de venta.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var pct = Math.Clamp(CommissionPercent, 0m, 1m);
        var amount = Math.Round((quote.EstimatedTotal ?? quote.SubtotalAuto) * pct, 2);

        var opp = new SalesOpportunity
        {
            QuoteRequestId = QuoteId,
            SellerProfileId = resolvedSellerProfileId,
            ClientId = quote.ClientId,
            Status = SalesOpportunityStatus.Prospect,
            WorkflowStage = SalesWorkflowStage.Lead,
            StageChangedAt = DateTime.UtcNow,
            StageDueAt = DateTime.UtcNow.Date.AddDays(2),
            CommissionPercent = pct,
            CommissionAmount = amount,
            Notes = (OpportunityNotes ?? "").Trim(),
            OwnerUserId = await _db.SalesSellerProfiles
                .Where(x => x.Id == resolvedSellerProfileId)
                .Select(x => x.EmployeeUserId)
                .FirstOrDefaultAsync(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.SalesOpportunities.Add(opp);
        await _db.SaveChangesAsync();
        await AddSalesAuditAsync(opp.Id, "opportunity.create", "Se creo oportunidad comercial desde cotización.");

        Flash = "Oportunidad creada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloseWonAsync(Guid id)
    {
        await EnsurePermissionsAsync();
        if (!CanManage)
        {
            Flash = "No tienes permiso para cerrar oportunidades.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var opp = await _db.SalesOpportunities
            .Include(x => x.QuoteRequest)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (opp == null) return RedirectToPage();

        opp.Status = SalesOpportunityStatus.ClosedWon;
        opp.WorkflowStage = SalesWorkflowStage.ClosedWon;
        opp.ClosedAt = DateTime.UtcNow;
        opp.StageChangedAt = DateTime.UtcNow;
        opp.StageDueAt = null;
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
        await AddSalesAuditAsync(opp.Id, "opportunity.closed.won", "Venta cerrada como ganada.");
        Flash = "Venta marcada como cerrada (won).";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkContractSignedAsync(Guid id)
    {
        await EnsurePermissionsAsync();
        if (!CanAssign)
        {
            Flash = "No tienes permiso para aplicar comisión.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var opp = await _db.SalesOpportunities
            .Include(x => x.SellerProfile!)
            .ThenInclude(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (opp == null) return RedirectToPage();
        if (opp.SellerProfile == null || opp.SellerProfile.Employee == null) return RedirectToPage();

        opp.Status = SalesOpportunityStatus.ContractSigned;
        opp.WorkflowStage = SalesWorkflowStage.Commission;
        opp.ContractSignedAt = DateTime.UtcNow;
        opp.StageChangedAt = DateTime.UtcNow;
        opp.StageDueAt = DateTime.UtcNow.Date.AddDays(2);
        opp.UpdatedAt = DateTime.UtcNow;

        if (!opp.BonusDeductionId.HasValue)
        {
            var (startDate, endDate) = NextBiweeklyPeriod(DateTime.Today);
            var deduction = new EmployeeDeduction
            {
                UserId = opp.SellerProfile.EmployeeUserId,
                Type = EmployeeDeductionType.ComisionVenta,
                Direction = EmployeeDeductionDirection.Bonus,
                Concept = $"Comisión de venta {opp.QuoteRequestId:N}",
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
            await SendCommissionEmailAsync(opp);
        }

        opp.Status = SalesOpportunityStatus.CommissionApplied;
        await _db.SaveChangesAsync();
        await AddSalesAuditAsync(opp.Id, "commission.applied", $"Comisión aplicada por {opp.CommissionAmount:C2}.");
        Flash = "Contrato firmado y comision aplicada a proxima nomina.";
        FlashType = "success";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var userId = _userMgr.GetUserId(User) ?? string.Empty;
        var employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new { x.UserId, Label = x.FullName + " · " + x.Email })
            .ToListAsync();
        EmployeeItems = new SelectList(employees, "UserId", "Label");

        var sellerQuery = _db.SalesSellerProfiles
            .AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => x.IsActive);
        if (!CanViewAll && !string.IsNullOrWhiteSpace(userId))
            sellerQuery = sellerQuery.Where(x => x.EmployeeUserId == userId);

        Sellers = await sellerQuery
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SellerRow(
                x.Id,
                x.Employee != null ? x.Employee.FullName : x.EmployeeUserId,
                x.Employee != null ? x.Employee.Email : "-",
                x.DefaultCommissionPercent,
                x.IsActive
            ))
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            CurrentSellerProfileId = await _db.SalesSellerProfiles
                .AsNoTracking()
                .Where(x => x.IsActive && x.EmployeeUserId == userId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
            if (CurrentSellerProfileId.HasValue)
            {
                CurrentSellerName = await _db.SalesSellerProfiles
                    .AsNoTracking()
                    .Where(x => x.Id == CurrentSellerProfileId.Value)
                    .Select(x => x.Employee != null ? x.Employee.FullName : x.EmployeeUserId)
                    .FirstOrDefaultAsync() ?? "";
            }
        }
        SellerItems = new SelectList(Sellers, "Id", "EmployeeName");

        var quotesQuery = _db.QuoteRequests
            .AsNoTracking()
            .Where(x => x.Status != QuoteRequestStatus.Rejected)
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        if (!CanViewAll)
        {
            var allowedQuoteIds = _db.SalesOpportunities
                .Where(o => o.OwnerUserId == userId || (o.SellerProfile != null && o.SellerProfile.EmployeeUserId == userId))
                .Select(o => o.QuoteRequestId);
            quotesQuery = quotesQuery.Where(x => allowedQuoteIds.Contains(x.Id));
        }

        var quotes = await quotesQuery
            .Take(300)
            .Select(x => new { x.Id, Label = x.Folio + " · " + x.CustomerName })
            .ToListAsync();
        QuoteItems = new SelectList(quotes, "Id", "Label");

        var oppQuery = _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Include(x => x.SellerProfile!).ThenInclude(x => x.Employee)
            .Include(x => x.Client)
            .AsQueryable();

        if (!CanViewAll)
            oppQuery = oppQuery.Where(x => x.OwnerUserId == userId || (x.SellerProfile != null && x.SellerProfile.EmployeeUserId == userId));

        Opportunities = await oppQuery
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

    private async Task EnsurePermissionsAsync()
    {
        CanViewAll = AppRoles.IsGlobalAdmin(User);
        var canViewOwn = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesViewOwn);
        if (!canViewOwn)
        {
            CanManage = false;
            CanAssign = false;
            return;
        }

        CanManage = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesManage);
        CanAssign = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesWorkflowAssign);
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

    private async Task AddSalesAuditAsync(Guid opportunityId, string eventType, string details)
    {
        _db.SalesAuditLogs.Add(new SalesAuditLog
        {
            SalesOpportunityId = opportunityId,
            EventType = eventType,
            UserId = _userMgr.GetUserId(User),
            UserName = User.Identity?.Name ?? "-",
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task SendCommissionEmailAsync(SalesOpportunity opp)
    {
        var email = opp.SellerProfile?.Employee?.Email;
        if (string.IsNullOrWhiteSpace(email)) return;

        var folio = opp.QuoteRequest?.Folio ?? opp.QuoteRequestId.ToString("N");
        var sellerName = opp.SellerProfile?.Employee?.FullName ?? "Vendedor";
        var amount = opp.CommissionAmount.ToString("C2");

        var (subject, body) = await _templates.RenderAsync(
            "sales.commission.paid",
            $"Comisión registrada {folio}",
            $"<p>Hola {sellerName},</p><p>Tu comision por la venta {folio} fue registrada por <b>{amount}</b>.</p>",
            new Dictionary<string, string>
            {
                ["Folio"] = folio,
                ["Vendedor"] = sellerName,
                ["MontoComisión"] = amount
            });

        await _email.SendAsync(email!, subject, body);
    }
}


