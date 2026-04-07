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
    private static readonly SalesWorkflowStage[] WorkflowStages =
    [
        SalesWorkflowStage.Lead,
        SalesWorkflowStage.Quotation,
        SalesWorkflowStage.Closing,
        SalesWorkflowStage.ClosedWon,
        SalesWorkflowStage.ClosedLost
    ];

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
    [BindProperty] public string NoteText { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string StageFilter { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string OwnerFilterUserId { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string Month { get; set; } = DateTime.UtcNow.ToString("yyyy-MM");

    public bool CanViewAll { get; set; }
    public bool CanMove { get; set; }
    public bool CanAssign { get; set; }

    public SelectList StageItems { get; set; } = default!;
    public SelectList SellerItems { get; set; } = default!;
    public SelectList OwnerItems { get; set; } = default!;
    public SelectList StageFilterItems { get; set; } = default!;
    public SelectList OwnerFilterItems { get; set; } = default!;

    public int TotalDeals { get; set; }
    public int OverdueDeals { get; set; }
    public int DueSoonDeals { get; set; }
    public int WonDeals { get; set; }

    public record DealVm(
        Guid Id,
        Guid? ClientId,
        string Folio,
        string Customer,
        string Seller,
        string? OwnerUserId,
        string Owner,
        decimal Total,
        SalesWorkflowStage Stage,
        DateTime UpdatedAt,
        DateTime? StageDueAt,
        bool IsOverdue,
        bool IsToday,
        bool IsSoon);

    public record NoteVm(DateTime CreatedAt, string UserName, string Text);
    public record DealDetailsVm(Guid OpportunityId, string Folio, string Customer, string Seller, string Owner, string Status, decimal Total, List<NoteVm> Notes);

    public Dictionary<SalesWorkflowStage, List<DealVm>> Board { get; set; } = new();
    public Dictionary<Guid, DealDetailsVm> DetailsByOpportunityId { get; set; } = new();

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
            return RedirectToPage(new { StageFilter, OwnerFilterUserId, Month });
        }

        var userId = _userMgr.GetUserId(User) ?? "";
        var opp = await ScopedOppQuery(userId, CanViewAll)
            .Include(x => x.Client)
            .Include(x => x.QuoteRequest)
            .FirstOrDefaultAsync(x => x.Id == OpportunityId);
        if (opp == null) return NotFound();

        if (!WorkflowStages.Contains(NewStage))
        {
            Flash = "Etapa no valida para el workflow comercial.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var old = opp.WorkflowStage;
        opp.WorkflowStage = NewStage;
        opp.StageChangedAt = DateTime.UtcNow;
        opp.StageDueAt = DateTime.UtcNow.Date.AddDays(SlaDays(NewStage));
        opp.UpdatedAt = DateTime.UtcNow;

        if (NewStage == SalesWorkflowStage.ClosedWon)
        {
            opp.Status = SalesOpportunityStatus.ClosedWon;
            opp.ClosedAt = DateTime.UtcNow;

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
        }
        if (NewStage == SalesWorkflowStage.ClosedLost)
        {
            opp.Status = SalesOpportunityStatus.ClosedLost;
            if (opp.QuoteRequest != null)
            {
                opp.QuoteRequest.Status = QuoteRequestStatus.Rejected;
                opp.QuoteRequest.AcceptedAt = DateTime.UtcNow;
                opp.QuoteRequest.AcceptedByUserId = User.Identity?.Name;
            }
        }
        if (NewStage == SalesWorkflowStage.Lead || NewStage == SalesWorkflowStage.Quotation || NewStage == SalesWorkflowStage.Closing)
            opp.Status = SalesOpportunityStatus.Prospect;

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
        return RedirectToPage(new { StageFilter, OwnerFilterUserId, Month });
    }

    public async Task<IActionResult> OnPostAddNoteAsync()
    {
        if (!await EnsurePermissionsAsync()) return Forbid();

        var userId = _userMgr.GetUserId(User) ?? "";
        var opp = await ScopedOppQuery(userId, CanViewAll).FirstOrDefaultAsync(x => x.Id == OpportunityId);
        if (opp == null) return NotFound();

        var text = (NoteText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            Flash = "Escribe una nota para guardar.";
            FlashType = "warning";
            return RedirectToPage(new { StageFilter, OwnerFilterUserId, Month });
        }

        if (text.Length > 2000)
            text = text[..2000];

        _db.SalesAuditLogs.Add(new SalesAuditLog
        {
            SalesOpportunityId = opp.Id,
            EventType = "workflow.note",
            UserId = userId,
            UserName = User.Identity?.Name ?? "-",
            PreviousStage = opp.WorkflowStage,
            NewStage = opp.WorkflowStage,
            Details = text,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Flash = "Nota guardada.";
        FlashType = "success";
        return RedirectToPage(new { StageFilter, OwnerFilterUserId, Month });
    }

    public async Task<IActionResult> OnPostAssignAsync()
    {
        Flash = "La asignación de owner/vendedor se define al crear la oportunidad.";
        FlashType = "info";
        return RedirectToPage(new { StageFilter, OwnerFilterUserId, Month });
    }

    private async Task<bool> EnsurePermissionsAsync()
    {
        var hasViewAll = User.IsInRole(AppRoles.SuperAdmin);
        var hasViewOwn = hasViewAll || await _actions.HasActionAsync(User, AppActions.SalesViewOwn);
        CanMove = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesWorkflowMove);
        CanAssign = false;

        CanViewAll = hasViewAll;
        return hasViewOwn;
    }

    private async Task LoadAsync()
    {
        var userId = _userMgr.GetUserId(User) ?? "";
        IQueryable<SalesOpportunity> query = ScopedOppQuery(userId, CanViewAll)
            .Include(x => x.QuoteRequest)
            .Include(x => x.SellerProfile!).ThenInclude(x => x.Employee);

        var (fromUtc, toUtc) = ResolveMonthRange();
        query = query.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtc);

        var rows = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();

        StageItems = new SelectList(WorkflowStages
            .Select(x => new { Value = x, Label = StageLabel(x) }), "Value", "Label");
        StageFilterItems = new SelectList(
            new[] { new { Value = "all", Label = "Todas las etapas" } }
                .Concat(WorkflowStages
                    .Select(x => new { Value = x.ToString(), Label = StageLabel(x) })),
            "Value",
            "Label",
            StageFilter);

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
        OwnerFilterItems = new SelectList(
            new[] { new { UserId = "all", Label = "Todos los owners" } }.Concat(owners),
            "UserId",
            "Label",
            OwnerFilterUserId);

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
                x.OwnerUserId,
                !string.IsNullOrWhiteSpace(x.OwnerUserId) && byUser.TryGetValue(x.OwnerUserId, out var ownerName) ? ownerName : "Sin owner",
                x.QuoteRequest?.EstimatedTotal ?? x.QuoteRequest?.SubtotalAuto ?? 0m,
                NormalizeStage(x.WorkflowStage),
                x.UpdatedAt,
                x.StageDueAt,
                overdue,
                isToday,
                isSoon);
        }).ToList();

        if (!string.IsNullOrWhiteSpace(StageFilter)
            && !string.Equals(StageFilter, "all", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<SalesWorkflowStage>(StageFilter, true, out var sf))
        {
            data = data.Where(d => d.Stage == sf).ToList();
        }

        if (!string.IsNullOrWhiteSpace(OwnerFilterUserId)
            && !string.Equals(OwnerFilterUserId, "all", StringComparison.OrdinalIgnoreCase))
        {
            data = data.Where(d => string.Equals(d.OwnerUserId, OwnerFilterUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        TotalDeals = data.Count;
        OverdueDeals = data.Count(d => d.IsOverdue);
        DueSoonDeals = data.Count(d => d.IsSoon || d.IsToday);
        WonDeals = data.Count(d => d.Stage == SalesWorkflowStage.ClosedWon);
        Board = WorkflowStages.ToDictionary(s => s, s => data.Where(d => d.Stage == s).ToList());

        var ids = rows.Select(x => x.Id).ToList();
        var notes = await _db.SalesAuditLogs
            .AsNoTracking()
            .Where(x => ids.Contains(x.SalesOpportunityId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(1200)
            .ToListAsync();

        DetailsByOpportunityId = rows.ToDictionary(
            x => x.Id,
            x =>
            {
                var n = notes.Where(a => a.SalesOpportunityId == x.Id && a.EventType == "workflow.note")
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(20)
                    .Select(a => new NoteVm(a.CreatedAt, a.UserName, a.Details))
                    .ToList();
                return new DealDetailsVm(
                    x.Id,
                    x.QuoteRequest?.Folio ?? "-",
                    x.QuoteRequest?.CustomerName ?? "-",
                    x.SellerProfile?.Employee?.FullName ?? "Sin vendedor",
                    !string.IsNullOrWhiteSpace(x.OwnerUserId) && byUser.TryGetValue(x.OwnerUserId, out var ownerName) ? ownerName : "Sin owner",
                    x.Status.ToString(),
                    x.QuoteRequest?.EstimatedTotal ?? x.QuoteRequest?.SubtotalAuto ?? 0m,
                    n);
            });
    }

    private IQueryable<SalesOpportunity> ScopedOppQuery(string userId, bool viewAll)
    {
        var q = _db.SalesOpportunities.AsQueryable();
        if (!viewAll)
            q = q.Where(x => x.OwnerUserId == userId || (x.SellerProfile != null && x.SellerProfile.EmployeeUserId == userId));
        return q;
    }

    private (DateTime fromUtc, DateTime toUtc) ResolveMonthRange()
    {
        if (!DateTime.TryParse($"{Month}-01", out var monthStart))
            monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        monthStart = DateTime.SpecifyKind(monthStart, DateTimeKind.Utc);
        return (monthStart, monthStart.AddMonths(1));
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

    public static int SlaDays(SalesWorkflowStage stage) => stage switch
    {
        SalesWorkflowStage.Lead => 2,
        SalesWorkflowStage.Quotation => 3,
        SalesWorkflowStage.Closing => 4,
        SalesWorkflowStage.ClosedWon => 0,
        SalesWorkflowStage.ClosedLost => 0,
        _ => 0
    };

    public static string StageLabel(SalesWorkflowStage stage) => stage switch
    {
        SalesWorkflowStage.Lead => "Oportunidad",
        SalesWorkflowStage.Quotation => "Cotización",
        SalesWorkflowStage.Closing => "Cierre",
        SalesWorkflowStage.ClosedWon => "Ganado",
        SalesWorkflowStage.ClosedLost => "Perdido",
        _ => stage.ToString()
    };

    private static SalesWorkflowStage NormalizeStage(SalesWorkflowStage current) => current switch
    {
        SalesWorkflowStage.Contract => SalesWorkflowStage.Closing,
        SalesWorkflowStage.Signature => SalesWorkflowStage.Closing,
        SalesWorkflowStage.Billing => SalesWorkflowStage.Closing,
        SalesWorkflowStage.Commission => SalesWorkflowStage.Closing,
        _ => current
    };
}
