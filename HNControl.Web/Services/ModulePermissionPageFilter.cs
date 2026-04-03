using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Services;

/// <summary>
/// Bloquea el acceso a módulos para Employees según su PermissionRole.
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

        // Admin bypass
        if (AppRoles.IsGlobalAdmin(user))
        {
            await next();
            return;
        }

        // Resolver módulo por ruta de Razor Pages
        var viewPath = (context.ActionDescriptor as PageActionDescriptor)?.ViewEnginePath;
        var moduleKey = AppModules.FromPagePath(viewPath);
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

        var actionKey = ResolveActionKey(moduleKey, viewPath, context.HttpContext.Request.Method);
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

    private static string? ResolveActionKey(string moduleKey, string? viewPath, string? method)
    {
        var isWrite = !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
        var path = viewPath ?? "";

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
            if (path.Contains("/Create", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Edit", StringComparison.OrdinalIgnoreCase))
                return AppActions.ProjectsEdit;
            return isWrite ? AppActions.ProjectsEdit : AppActions.ProjectsView;
        }

        return null;
    }
}
