using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Services;

/// <summary>
/// Bloquea el acceso a modulos para Employees segun su PermissionRole.
/// Admin bypass.
/// </summary>
public class ModulePermissionPageFilter : IAsyncPageFilter
{
    private readonly IModuleAccessService _access;
    private readonly IActionAccessService _actions;

    public ModulePermissionPageFilter(IModuleAccessService access, IActionAccessService actions)
    {
        _access = access;
        _actions = actions;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        if (AppRoles.IsGlobalAdmin(user))
        {
            await next();
            return;
        }

        var viewPath = (context.ActionDescriptor as PageActionDescriptor)?.ViewEnginePath;
        var moduleKey = AppModules.FromPagePath(viewPath);
        var isLeadsView = string.Equals(context.HttpContext.Request.Query["View"], "leads", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(viewPath, "/Clients/Index", StringComparison.OrdinalIgnoreCase) && isLeadsView)
            moduleKey = AppModules.Sales;

        if (moduleKey == null)
        {
            await next();
            return;
        }

        var allowed = await _access.HasAccessAsync(user, moduleKey);
        if (!allowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        var actionKey = ResolveActionKey(
            moduleKey,
            viewPath,
            context.HttpContext.Request.Method,
            context.HandlerMethod?.MethodInfo?.Name,
            isLeadsView);

        if (!string.IsNullOrWhiteSpace(actionKey))
        {
            var hasAction = await _actions.HasActionAsync(user, actionKey);
            if (!hasAction)
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }

    private static string? ResolveActionKey(string moduleKey, string? viewPath, string? method, string? handlerMethodName, bool isLeadsView)
    {
        var isWrite = !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
        var path = viewPath ?? "";
        var handler = handlerMethodName ?? "";

        if (string.Equals(moduleKey, AppModules.Clients, StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains("/Create", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Edit", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Services", StringComparison.OrdinalIgnoreCase))
                return AppActions.ClientsEdit;
            return isWrite ? AppActions.ClientsEdit : AppActions.ClientsView;
        }

        if (string.Equals(moduleKey, AppModules.Projects, StringComparison.OrdinalIgnoreCase))
        {
            if (path.StartsWith("/Projects/Investments", StringComparison.OrdinalIgnoreCase))
                return isWrite ? AppActions.ProjectsInvestmentsEdit : AppActions.ProjectsInvestmentsView;
            if (path.StartsWith("/Projects/Resellers", StringComparison.OrdinalIgnoreCase))
                return isWrite ? AppActions.ProjectsResellersEdit : AppActions.ProjectsResellersView;
            if (path.StartsWith("/Projects/Delivery", StringComparison.OrdinalIgnoreCase))
                return isWrite ? AppActions.ProjectsDeliveryEdit : AppActions.ProjectsDeliveryView;

            if (path.Contains("/Create", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Edit", StringComparison.OrdinalIgnoreCase))
                return AppActions.ProjectsEdit;
            return isWrite ? AppActions.ProjectsEdit : AppActions.ProjectsView;
        }

        if (string.Equals(moduleKey, AppModules.Inventory, StringComparison.OrdinalIgnoreCase))
        {
            if (path.StartsWith("/Admin/Inventory/Approvals", StringComparison.OrdinalIgnoreCase))
                return AppActions.InventoryApprove;

            if (path.StartsWith("/Admin/Inventory", StringComparison.OrdinalIgnoreCase))
                return AppActions.InventoryManage;

            return isWrite ? AppActions.InventoryManage : AppActions.InventoryView;
        }

        if (string.Equals(moduleKey, AppModules.Tickets, StringComparison.OrdinalIgnoreCase))
        {
            if (handler.Contains("Close", StringComparison.OrdinalIgnoreCase))
                return AppActions.TicketsClose;

            return isWrite ? AppActions.TicketsManage : AppActions.TicketsView;
        }

        if (string.Equals(moduleKey, AppModules.Sales, StringComparison.OrdinalIgnoreCase))
        {
            if (path.StartsWith("/Clients/Index", StringComparison.OrdinalIgnoreCase)
                && isLeadsView)
            {
                if (handler.Contains("ConvertLead", StringComparison.OrdinalIgnoreCase))
                    return AppActions.SalesProspectsConvert;
                if (handler.Contains("CreateLead", StringComparison.OrdinalIgnoreCase))
                    return AppActions.SalesProspectsCreate;
                if (isWrite)
                    return AppActions.SalesProspectsEdit;
                return AppActions.SalesProspectsView;
            }

            if (path.StartsWith("/Sales/Templates", StringComparison.OrdinalIgnoreCase))
                return AppActions.TemplatesManage;

            if (path.StartsWith("/Sales/Prospects", StringComparison.OrdinalIgnoreCase))
            {
                if (handler.Contains("Convert", StringComparison.OrdinalIgnoreCase))
                    return AppActions.SalesProspectsConvert;
                if (handler.Contains("Create", StringComparison.OrdinalIgnoreCase))
                    return AppActions.SalesProspectsCreate;
                if (isWrite)
                    return AppActions.SalesProspectsEdit;
                return AppActions.SalesProspectsView;
            }

            if (path.StartsWith("/Sales/Calls", StringComparison.OrdinalIgnoreCase))
                return isWrite ? AppActions.SalesCallsUse : AppActions.SalesCallsView;

            if (path.StartsWith("/Admin/Quotes", StringComparison.OrdinalIgnoreCase))
                return isWrite ? AppActions.SalesQuotesManage : AppActions.SalesQuotesView;

            if (path.StartsWith("/Projects/Sales", StringComparison.OrdinalIgnoreCase))
                return isWrite ? AppActions.SalesManage : AppActions.SalesViewAll;

            if (path.StartsWith("/Sales/Workflow", StringComparison.OrdinalIgnoreCase))
            {
                if (handler.Contains("Assign", StringComparison.OrdinalIgnoreCase))
                    return AppActions.SalesWorkflowAssign;
                if (isWrite)
                    return AppActions.SalesWorkflowMove;
            }

            return AppActions.SalesViewOwn;
        }

        if (string.Equals(moduleKey, AppModules.Carriers, StringComparison.OrdinalIgnoreCase))
            return isWrite ? AppActions.CarriersManage : AppActions.CarriersView;

        if (string.Equals(moduleKey, AppModules.Monitoring, StringComparison.OrdinalIgnoreCase))
            return isWrite ? AppActions.MonitoringManage : AppActions.MonitoringView;

        if (string.Equals(moduleKey, AppModules.Billing, StringComparison.OrdinalIgnoreCase))
        {
            if (handler.Contains("MarkSent", StringComparison.OrdinalIgnoreCase))
                return AppActions.BillingSend;
            return isWrite ? AppActions.BillingManage : AppActions.BillingViewOwn;
        }

        return null;
    }
}
