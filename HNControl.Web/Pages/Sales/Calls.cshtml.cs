using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class CallsModel : PageModel
{
    private readonly IActionAccessService _actions;

    public CallsModel(IActionAccessService actions)
    {
        _actions = actions;
    }

    public bool CanViewAll { get; private set; }
    public bool CanUseCalls { get; private set; }
    public string ExternalSoftphoneUrl { get; } = "https://cm.ucc.systems/connect/app";
    public Guid? OpportunityId { get; private set; }
    public string? OpportunityFolio { get; private set; }
    public string? OpportunityCustomer { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? opportunityId, string? folio, string? customer)
    {
        var hasViewAll = AppRoles.IsGlobalAdmin(User)
            || await _actions.HasActionAsync(User, AppActions.SalesViewAll);
        var canViewCalls = hasViewAll
            || await _actions.HasActionAsync(User, AppActions.SalesCallsView);

        if (!canViewCalls)
            return Forbid();

        CanViewAll = hasViewAll;
        CanUseCalls = hasViewAll
            || await _actions.HasActionAsync(User, AppActions.SalesCallsUse)
            || canViewCalls;

        OpportunityId = opportunityId;
        OpportunityFolio = string.IsNullOrWhiteSpace(folio) ? null : folio.Trim();
        OpportunityCustomer = string.IsNullOrWhiteSpace(customer) ? null : customer.Trim();

        return Page();
    }
}
