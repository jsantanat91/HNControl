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

    public ModulePermissionPageFilter(IModuleAccessService access)
    {
        _access = access;
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

        await next();
    }
}
