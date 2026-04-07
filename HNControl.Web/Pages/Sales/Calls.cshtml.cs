using System.ComponentModel.DataAnnotations;
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
public class CallsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IActionAccessService _actions;
    private readonly ISecretProtector _protector;

    public CallsModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userMgr,
        IActionAccessService actions,
        ISecretProtector protector)
    {
        _db = db;
        _userMgr = userMgr;
        _actions = actions;
        _protector = protector;
    }

    public record OpportunityVm(Guid Id, string Label);
    public record CallVm(
        DateTime CreatedAt,
        string Seller,
        string Opportunity,
        string Number,
        string Result,
        int DurationSeconds,
        string Notes);

    [BindProperty]
    public SipInput Sip { get; set; } = new();

    [BindProperty]
    public CallInput Call { get; set; } = new();
    [BindProperty(SupportsGet = true)]
    public Guid? OpportunityId { get; set; }

    public List<OpportunityVm> Opportunities { get; set; } = new();
    public List<CallVm> RecentCalls { get; set; } = new();

    public bool CanViewAll { get; set; }
    public bool CanUseCalls { get; set; }
    public bool HasStoredPassword { get; set; }
    public Guid? SelectedOpportunityId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        if (!await ResolvePermissionsAsync())
            return Forbid();

        await LoadPageDataAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveSipAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        if (!await ResolvePermissionsAsync())
            return Forbid();

        if (!CanUseCalls)
            return Forbid();

        Sip.Host = (Sip.Host ?? "").Trim();
        Sip.User = (Sip.User ?? "").Trim();

        if (string.IsNullOrWhiteSpace(Sip.Host))
            ModelState.AddModelError("Sip.Host", "Host es requerido.");
        if (string.IsNullOrWhiteSpace(Sip.User))
            ModelState.AddModelError("Sip.User", "Usuario SIP es requerido.");

        var account = await _db.SalesSipAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
        if (!ModelState.IsValid)
        {
            if (account != null)
            {
                HasStoredPassword = !string.IsNullOrWhiteSpace(account.SipPasswordProtected);
            }
            await LoadPageDataAsync(userId);
            return Page();
        }

        if (account == null)
        {
            account = new SalesSipAccount
            {
                UserId = userId,
                Host = Sip.Host,
                SipUser = Sip.User,
                SipPasswordProtected = string.IsNullOrWhiteSpace(Sip.Password) ? "" : _protector.Protect(Sip.Password),
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            };
            _db.SalesSipAccounts.Add(account);
        }
        else
        {
            account.Host = Sip.Host;
            account.SipUser = Sip.User;
            account.IsActive = true;
            account.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(Sip.Password))
                account.SipPasswordProtected = _protector.Protect(Sip.Password);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Extensión SIP guardada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogCallAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return new JsonResult(new { ok = false, message = "Sesión no válida." }) { StatusCode = 401 };

        if (!await ResolvePermissionsAsync() || !CanUseCalls)
            return new JsonResult(new { ok = false, message = "Sin permiso para registrar llamadas." }) { StatusCode = 403 };

        Call.DialedNumber = (Call.DialedNumber ?? "").Trim();
        Call.Note = (Call.Note ?? "").Trim();
        if (string.IsNullOrWhiteSpace(Call.DialedNumber))
            return new JsonResult(new { ok = false, message = "Número requerido." }) { StatusCode = 400 };

        var result = ParseResult(Call.Result);
        var duration = Math.Max(0, Math.Min(Call.DurationSeconds, 86400));

        Guid? opportunityId = null;
        if (Call.SalesOpportunityId.HasValue)
        {
            var oppId = Call.SalesOpportunityId.Value;
            var scoped = _db.SalesOpportunities.AsNoTracking().Where(x => x.Id == oppId);
            if (!CanViewAll)
            {
                scoped = scoped.Where(x => x.OwnerUserId == userId || (x.SellerProfile != null && x.SellerProfile.EmployeeUserId == userId));
            }

            if (await scoped.AnyAsync())
                opportunityId = oppId;
        }

        var log = new SalesCallLog
        {
            UserId = userId,
            SalesOpportunityId = opportunityId,
            DialedNumber = Call.DialedNumber,
            Result = result,
            DurationSeconds = duration,
            Notes = Limit(Call.Note, 2000),
            CreatedAt = DateTime.UtcNow
        };
        _db.SalesCallLogs.Add(log);

        if (opportunityId.HasValue)
        {
            _db.SalesAuditLogs.Add(new SalesAuditLog
            {
                SalesOpportunityId = opportunityId.Value,
                EventType = "call.log",
                UserId = userId,
                UserName = User.Identity?.Name ?? "-",
                Details = $"Llamada {ResultLabel(result)} · {log.DialedNumber} · {duration}s. {log.Notes}".Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return new JsonResult(new { ok = true });
    }

    private async Task<bool> ResolvePermissionsAsync()
    {
        var hasViewAll = AppRoles.IsGlobalAdmin(User)
            || await _actions.HasActionAsync(User, AppActions.SalesViewAll)
            || await _actions.HasActionAsync(User, AppActions.SalesManage);
        var canViewCalls = hasViewAll || await _actions.HasActionAsync(User, AppActions.SalesCallsView);

        CanViewAll = hasViewAll;
        CanUseCalls = hasViewAll || await _actions.HasActionAsync(User, AppActions.SalesCallsUse);
        return canViewCalls;
    }

    private async Task LoadPageDataAsync(string userId)
    {
        var oppQuery = _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Include(x => x.SellerProfile!).ThenInclude(x => x.Employee)
            .AsQueryable();
        if (!CanViewAll)
        {
            oppQuery = oppQuery.Where(x => x.OwnerUserId == userId || (x.SellerProfile != null && x.SellerProfile.EmployeeUserId == userId));
        }

        Opportunities = await oppQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(150)
            .Select(x => new OpportunityVm(
                x.Id,
                $"{(x.QuoteRequest != null ? x.QuoteRequest.Folio : "-")} · {(x.QuoteRequest != null ? x.QuoteRequest.CustomerName : "Sin cliente")} · {(x.SellerProfile != null && x.SellerProfile.Employee != null ? x.SellerProfile.Employee.FullName : "Sin vendedor")}"))
            .ToListAsync();

        if (OpportunityId.HasValue && Opportunities.Any(x => x.Id == OpportunityId.Value))
            SelectedOpportunityId = OpportunityId;

        var account = await _db.SalesSipAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
        if (account != null)
        {
            Sip.Host = account.Host;
            Sip.User = account.SipUser;
            HasStoredPassword = !string.IsNullOrWhiteSpace(account.SipPasswordProtected);
        }

        var logsQuery = _db.SalesCallLogs
            .AsNoTracking()
            .Include(x => x.SalesOpportunity!).ThenInclude(x => x.QuoteRequest)
            .AsQueryable();

        if (!CanViewAll)
        {
            logsQuery = logsQuery.Where(x => x.UserId == userId);
        }

        var logs = await logsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(60)
            .ToListAsync();

        var userIds = logs.Select(x => x.UserId).Distinct().ToList();
        var mapUsers = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.FullName);

        RecentCalls = logs.Select(x =>
        {
            var seller = mapUsers.TryGetValue(x.UserId, out var fullName) ? fullName : x.UserId;
            var opportunity = x.SalesOpportunity?.QuoteRequest?.Folio ?? "-";
            return new CallVm(
                x.CreatedAt,
                seller,
                opportunity,
                x.DialedNumber,
                ResultLabel(x.Result),
                x.DurationSeconds,
                x.Notes);
        }).ToList();
    }

    private static SalesCallResult ParseResult(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "connected" => SalesCallResult.Connected,
            "completed" => SalesCallResult.Completed,
            "failed" => SalesCallResult.Failed,
            "canceled" => SalesCallResult.Canceled,
            _ => SalesCallResult.Initiated
        };
    }

    private static string ResultLabel(SalesCallResult value) => value switch
    {
        SalesCallResult.Connected => "Conectada",
        SalesCallResult.Completed => "Completada",
        SalesCallResult.Failed => "Fallida",
        SalesCallResult.Canceled => "Cancelada",
        _ => "Iniciada"
    };

    private static string Limit(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max];
    }

    public class SipInput
    {
        [Required, MaxLength(220)]
        public string Host { get; set; } = "";

        [Required, MaxLength(180)]
        public string User { get; set; } = "";

        [MaxLength(180)]
        public string Password { get; set; } = "";
    }

    public class CallInput
    {
        public Guid? SalesOpportunityId { get; set; }
        [MaxLength(60)] public string DialedNumber { get; set; } = "";
        [MaxLength(40)] public string Result { get; set; } = "initiated";
        [MaxLength(2000)] public string Note { get; set; } = "";
        public int DurationSeconds { get; set; }
    }
}
