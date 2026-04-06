using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IActionAccessService _actions;

    public DashboardModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IActionAccessService actions)
    {
        _db = db;
        _userMgr = userMgr;
        _actions = actions;
    }

    public record StageStat(string Stage, int Count);
    public record SellerStat(string Seller, int Deals, decimal Amount, decimal Commission);
    public record AuditVm(DateTime CreatedAt, string UserName, string EventType, string Details);

    public bool CanViewAll { get; set; }
    public bool IsOwnScope { get; set; }

    public int LeadsOpen { get; set; }
    public int ClosedWon { get; set; }
    public int ClosedLost { get; set; }
    public decimal CloseRate { get; set; }
    public decimal AvgTicket { get; set; }
    public decimal CommissionProjected { get; set; }

    public List<StageStat> Funnel { get; set; } = new();
    public List<SellerStat> Sellers { get; set; } = new();
    public List<AuditVm> RecentAudit { get; set; } = new();

    public string FunnelLabelsJson { get; set; } = "[]";
    public string FunnelValuesJson { get; set; } = "[]";
    public string SellerLabelsJson { get; set; } = "[]";
    public string SellerValuesJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var hasViewAll = AppRoles.IsGlobalAdmin(User);
        var hasViewOwn = hasViewAll || await _actions.HasActionAsync(User, AppActions.SalesViewOwn);
        if (!hasViewOwn) return Forbid();

        CanViewAll = hasViewAll;
        IsOwnScope = !hasViewAll;

        var oppQuery = _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Include(x => x.SellerProfile!).ThenInclude(x => x.Employee)
            .AsQueryable();

        if (!hasViewAll)
        {
            oppQuery = oppQuery.Where(x => x.OwnerUserId == userId || (x.SellerProfile != null && x.SellerProfile.EmployeeUserId == userId));
        }

        var opps = await oppQuery.ToListAsync();

        LeadsOpen = opps.Count(x => x.WorkflowStage != SalesWorkflowStage.ClosedWon && x.WorkflowStage != SalesWorkflowStage.ClosedLost);
        ClosedWon = opps.Count(x => x.WorkflowStage == SalesWorkflowStage.ClosedWon);
        ClosedLost = opps.Count(x => x.WorkflowStage == SalesWorkflowStage.ClosedLost);

        var closedTotal = ClosedWon + ClosedLost;
        CloseRate = closedTotal > 0 ? Math.Round((decimal)ClosedWon * 100m / closedTotal, 2) : 0m;

        var wonTickets = opps
            .Where(x => x.WorkflowStage == SalesWorkflowStage.ClosedWon)
            .Select(x => x.QuoteRequest?.EstimatedTotal ?? x.QuoteRequest?.SubtotalAuto ?? 0m)
            .ToList();
        AvgTicket = wonTickets.Count > 0 ? Math.Round(wonTickets.Average(), 2) : 0m;

        CommissionProjected = opps
            .Where(x => x.WorkflowStage != SalesWorkflowStage.ClosedLost && !x.BonusDeductionId.HasValue)
            .Sum(x => x.CommissionAmount);

        Funnel = opps
            .GroupBy(x => x.WorkflowStage)
            .Select(g => new StageStat(StageLabel(g.Key), g.Count()))
            .OrderBy(x => StageSort(x.Stage))
            .ToList();

        Sellers = opps
            .GroupBy(x => x.SellerProfile != null && x.SellerProfile.Employee != null ? x.SellerProfile.Employee.FullName : "Sin vendedor")
            .Select(g => new SellerStat(
                g.Key,
                g.Count(),
                g.Sum(x => x.QuoteRequest?.EstimatedTotal ?? x.QuoteRequest?.SubtotalAuto ?? 0m),
                g.Sum(x => x.CommissionAmount)))
            .OrderByDescending(x => x.Amount)
            .Take(8)
            .ToList();

        var oppIds = opps.Select(x => x.Id).ToHashSet();
        RecentAudit = await _db.SalesAuditLogs
            .AsNoTracking()
            .Where(x => oppIds.Contains(x.SalesOpportunityId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new AuditVm(x.CreatedAt, x.UserName, x.EventType, x.Details))
            .ToListAsync();

        FunnelLabelsJson = JsonSerializer.Serialize(Funnel.Select(x => x.Stage).ToList());
        FunnelValuesJson = JsonSerializer.Serialize(Funnel.Select(x => x.Count).ToList());
        SellerLabelsJson = JsonSerializer.Serialize(Sellers.Select(x => x.Seller).ToList());
        SellerValuesJson = JsonSerializer.Serialize(Sellers.Select(x => x.Amount).ToList());

        return Page();
    }

    private static string StageLabel(SalesWorkflowStage stage) => stage switch
    {
        SalesWorkflowStage.Lead => "Lead",
        SalesWorkflowStage.Quotation => "Cotizacion",
        SalesWorkflowStage.Closing => "Cierre",
        SalesWorkflowStage.Contract => "Contrato",
        SalesWorkflowStage.Signature => "Firma",
        SalesWorkflowStage.Billing => "Facturacion",
        SalesWorkflowStage.Commission => "Comision",
        SalesWorkflowStage.ClosedWon => "Ganado",
        SalesWorkflowStage.ClosedLost => "Perdido",
        _ => stage.ToString()
    };

    private static int StageSort(string stage) => stage switch
    {
        "Lead" => 1,
        "Cotizacion" => 2,
        "Cierre" => 3,
        "Contrato" => 4,
        "Firma" => 5,
        "Facturacion" => 6,
        "Comision" => 7,
        "Ganado" => 8,
        "Perdido" => 9,
        _ => 99
    };
}
