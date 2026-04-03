using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class WorkflowModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IActionAccessService _actions;

    public WorkflowModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IActionAccessService actions)
    {
        _db = db;
        _userMgr = userMgr;
        _actions = actions;
    }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    [BindProperty] public Guid OpportunityId { get; set; }
    [BindProperty] public SalesWorkflowStage NewStage { get; set; }
    [BindProperty] public Guid? AssignSellerProfileId { get; set; }
    [BindProperty] public string AssignOwnerUserId { get; set; } = "";

    public bool CanViewAll { get; set; }
    public bool CanMove { get; set; }
    public bool CanAssign { get; set; }

    public SelectList StageItems { get; set; } = default!;
    public SelectList SellerItems { get; set; } = default!;
    public SelectList OwnerItems { get; set; } = default!;

    public record DealVm(
        Guid Id,
        Guid? ClientId,
        string Folio,
        string Customer,
        string Seller,
        string Owner,
        decimal Total,
        SalesWorkflowStage Stage,
        DateTime UpdatedAt,
        DateTime? StageDueAt,
        bool IsOverdue,
        bool IsToday,
        bool IsSoon);

    public Dictionary<SalesWorkflowStage, List<DealVm>> Board { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await EnsurePermissionsAsync()) return Forbid();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMoveStageAsync()
    {
        if (!await EnsurePermissionsAsync()) return Forbid();
        if (!CanMove)
        {
            Flash = "No tienes permiso para mover etapas.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var userId = _userMgr.GetUserId(User) ?? "";
        var opp = await ScopedOppQuery(userId, CanViewAll)
            .FirstOrDefaultAsync(x => x.Id == OpportunityId);
        if (opp == null) return NotFound();

        var old = opp.WorkflowStage;
        opp.WorkflowStage = NewStage;
        opp.StageChangedAt = DateTime.UtcNow;
        opp.StageDueAt = DateTime.UtcNow.Date.AddDays(SlaDays(NewStage));
        opp.UpdatedAt = DateTime.UtcNow;

        if (NewStage == SalesWorkflowStage.ClosedWon) opp.Status = SalesOpportunityStatus.ClosedWon;
        if (NewStage == SalesWorkflowStage.ClosedLost) opp.Status = SalesOpportunityStatus.ClosedLost;

        _db.SalesAuditLogs.Add(new SalesAuditLog
        {
            SalesOpportunityId = opp.Id,
            EventType = "workflow.move",
            UserId = userId,
            UserName = User.Identity?.Name ?? "-",
            PreviousStage = old,
            NewStage = NewStage,
            Details = $"Etapa: {old} -> {NewStage}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        Flash = "Etapa actualizada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignAsync()
    {
        if (!await EnsurePermissionsAsync()) return Forbid();
        if (!CanAssign)
        {
            Flash = "No tienes permiso para asignar.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var userId = _userMgr.GetUserId(User) ?? "";
        var opp = await ScopedOppQuery(userId, CanViewAll)
            .FirstOrDefaultAsync(x => x.Id == OpportunityId);
        if (opp == null) return NotFound();

        var beforeOwner = opp.OwnerUserId;
        var beforeSeller = opp.SellerProfileId;

        if (!string.IsNullOrWhiteSpace(AssignOwnerUserId))
            opp.OwnerUserId = AssignOwnerUserId.Trim();
        if (AssignSellerProfileId.HasValue)
            opp.SellerProfileId = AssignSellerProfileId;

        opp.UpdatedAt = DateTime.UtcNow;

        _db.SalesAuditLogs.Add(new SalesAuditLog
        {
            SalesOpportunityId = opp.Id,
            EventType = "workflow.assign",
            UserId = userId,
            UserName = User.Identity?.Name ?? "-",
            Details = $"Owner: {beforeOwner ?? "-"} -> {opp.OwnerUserId ?? "-"}; Seller: {(beforeSeller?.ToString() ?? "-")} -> {(opp.SellerProfileId?.ToString() ?? "-")}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        Flash = "Asignacion actualizada.";
        FlashType = "success";
        return RedirectToPage();
    }

    private async Task<bool> EnsurePermissionsAsync()
    {
        var hasViewAll = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesViewAll);
        var hasViewOwn = hasViewAll || await _actions.HasActionAsync(User, AppActions.SalesViewOwn);
        CanMove = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesWorkflowMove);
        CanAssign = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesWorkflowAssign);

        CanViewAll = hasViewAll;
        return hasViewOwn;
    }

    private async Task LoadAsync()
    {
        var userId = _userMgr.GetUserId(User) ?? "";
        var query = ScopedOppQuery(userId, CanViewAll)
            .Include(x => x.QuoteRequest)
            .Include(x => x.SellerProfile!).ThenInclude(x => x.Employee)
            .OrderByDescending(x => x.UpdatedAt);

        var rows = await query.ToListAsync();

        StageItems = new SelectList(Enum.GetValues<SalesWorkflowStage>()
            .Select(x => new { Value = x, Label = StageLabel(x) }), "Value", "Label");

        var sellers = await _db.SalesSellerProfiles.AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Employee != null ? x.Employee.FullName : x.EmployeeUserId)
            .Select(x => new { x.Id, Label = x.Employee != null ? x.Employee.FullName : x.EmployeeUserId })
            .ToListAsync();
        SellerItems = new SelectList(sellers, "Id", "Label");

        var owners = await _db.EmployeeProfiles.AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new { x.UserId, Label = x.FullName + " - " + x.Email })
            .ToListAsync();
        OwnerItems = new SelectList(owners, "UserId", "Label");

        var byUser = owners.ToDictionary(x => x.UserId, x => x.Label, StringComparer.OrdinalIgnoreCase);

        var data = rows.Select(x =>
        {
            var due = x.StageDueAt?.Date;
            var today = DateTime.UtcNow.Date;
            var overdue = due.HasValue && due.Value < today;
            var isToday = due.HasValue && due.Value == today;
            var isSoon = due.HasValue && due.Value > today && due.Value <= today.AddDays(2);
            return new DealVm(
                x.Id,
                x.ClientId,
                x.QuoteRequest?.Folio ?? "-",
                x.QuoteRequest?.CustomerName ?? "-",
                x.SellerProfile?.Employee?.FullName ?? "Sin vendedor",
                !string.IsNullOrWhiteSpace(x.OwnerUserId) && byUser.TryGetValue(x.OwnerUserId, out var ownerName) ? ownerName : "Sin owner",
                x.QuoteRequest?.EstimatedTotal ?? x.QuoteRequest?.SubtotalAuto ?? 0m,
                x.WorkflowStage,
                x.UpdatedAt,
                x.StageDueAt,
                overdue,
                isToday,
                isSoon);
        }).ToList();

        var stages = Enum.GetValues<SalesWorkflowStage>();
        Board = stages.ToDictionary(s => s, s => data.Where(d => d.Stage == s).ToList());
    }

    private IQueryable<SalesOpportunity> ScopedOppQuery(string userId, bool viewAll)
    {
        var q = _db.SalesOpportunities.AsQueryable();
        if (!viewAll)
            q = q.Where(x => x.OwnerUserId == userId || (x.SellerProfile != null && x.SellerProfile.EmployeeUserId == userId));
        return q;
    }

    public static int SlaDays(SalesWorkflowStage stage) => stage switch
    {
        SalesWorkflowStage.Lead => 2,
        SalesWorkflowStage.Quotation => 3,
        SalesWorkflowStage.Closing => 4,
        SalesWorkflowStage.Contract => 3,
        SalesWorkflowStage.Signature => 2,
        SalesWorkflowStage.Billing => 2,
        SalesWorkflowStage.Commission => 2,
        _ => 0
    };

    public static string StageLabel(SalesWorkflowStage stage) => stage switch
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
}
