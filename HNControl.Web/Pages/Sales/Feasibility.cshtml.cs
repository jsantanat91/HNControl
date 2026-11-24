using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

/// <summary>Factibilidades (2.6.0): technical surveys received from the public questionnaire, by client.</summary>
[Authorize(Policy = "EmployeeOnly")]
public class FeasibilityModel(ApplicationDbContext db, IActionAccessService actions) : PageModel
{
    public const int PageSize = 40;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public LevantamientoSurveyStatus? Status { get; set; }
    /// <summary>Client id, or "sin" for surveys without a client.</summary>
    [BindProperty(SupportsGet = true)] public string? Cliente { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public List<RowVm> Rows { get; set; } = [];
    public int Total { get; set; }
    public Dictionary<LevantamientoSurveyStatus, int> Counts { get; set; } = [];
    public int Unlinked { get; set; }
    public int AllCount { get; set; }
    public List<(Guid Id, string Label, int Count)> ClientOptions { get; set; } = [];
    public string? FilteredClientName { get; set; }
    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public record RowVm(Guid Id, DateTime ReceivedAt, string Company, string Rfc, string ContactName, string Phone, string Email,
        string Services, string Urgency, LevantamientoSurveyStatus Status, Guid? ClientId, string? ClientName, string? ClientCode,
        bool HasOrder, bool HasSites, string City);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanViewAsync(User, actions)) return Forbid();

        var all = db.LevantamientoSurveys.AsNoTracking();
        Counts = await all.GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        AllCount = Counts.Values.Sum();
        Unlinked = await all.CountAsync(x => x.ClientId == null);

        var clients = await all.Where(x => x.ClientId != null)
            .GroupBy(x => new { x.ClientId, x.Client!.Name, x.Client.ClientCode })
            .Select(g => new { g.Key.ClientId, g.Key.Name, g.Key.ClientCode, N = g.Count() })
            .OrderBy(x => x.Name).ToListAsync();
        ClientOptions = clients.Select(x => (x.ClientId!.Value, $"{x.Name} · {x.ClientCode}", x.N)).ToList();

        var q = all;
        if (Status != null) q = q.Where(x => x.Status == Status);
        if (Cliente == "sin") q = q.Where(x => x.ClientId == null);
        else if (Guid.TryParse(Cliente, out var clientId))
        {
            q = q.Where(x => x.ClientId == clientId);
            FilteredClientName = await db.Clients.Where(x => x.Id == clientId).Select(x => x.Name).FirstOrDefaultAsync();
        }
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLower();
            var rfc = LevantamientoSurveyService.NormalizeId(Q);
            q = q.Where(x => x.Company.ToLower().Contains(term) || x.ContactName.ToLower().Contains(term)
                || x.Email.ToLower().Contains(term) || x.Services.ToLower().Contains(term) || (rfc.Length >= 3 && x.Rfc.Contains(rfc)));
        }

        Total = await q.CountAsync();
        PageNumber = Math.Clamp(PageNumber, 1, Math.Max(1, (Total + PageSize - 1) / PageSize));
        Rows = await q.OrderByDescending(x => x.ReceivedAt)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize)
            .Select(x => new RowVm(x.Id, x.ReceivedAt, x.Company, x.Rfc, x.ContactName, x.Phone, x.Email, x.Services, x.Urgency,
                x.Status, x.ClientId, x.Client != null ? x.Client.Name : null, x.Client != null ? x.Client.ClientCode : null,
                x.ServiceOrderId != null, x.SitesBytes != null, x.City))
            .ToListAsync();
        return Page();
    }

    public static async Task<bool> CanViewAsync(System.Security.Claims.ClaimsPrincipal user, IActionAccessService actions) =>
        AppRoles.IsGlobalAdmin(user) || await actions.HasActionAsync(user, AppActions.SalesFeasibilityView);

    public static string StatusLabel(LevantamientoSurveyStatus s) => s switch
    {
        LevantamientoSurveyStatus.New => "Nueva",
        LevantamientoSurveyStatus.InReview => "En revisión",
        LevantamientoSurveyStatus.Feasible => "Factible",
        LevantamientoSurveyStatus.NotFeasible => "No factible",
        LevantamientoSurveyStatus.Quoted => "Cotizada",
        _ => s.ToString()
    };

    public static string StatusBadge(LevantamientoSurveyStatus s) => s switch
    {
        LevantamientoSurveyStatus.New => "text-bg-primary",
        LevantamientoSurveyStatus.InReview => "text-bg-warning",
        LevantamientoSurveyStatus.Feasible => "text-bg-success",
        LevantamientoSurveyStatus.NotFeasible => "text-bg-danger",
        LevantamientoSurveyStatus.Quoted => "text-bg-info",
        _ => "text-bg-secondary"
    };
}
