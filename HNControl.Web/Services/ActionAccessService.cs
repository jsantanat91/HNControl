using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class ActionAccessService : IActionAccessService
{
    private const string CacheKey = "__hn_allowed_actions";
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IModuleAccessService _modules;

    public ActionAccessService(ApplicationDbContext db, IHttpContextAccessor http, IModuleAccessService modules)
    {
        _db = db;
        _http = http;
        _modules = modules;
    }

    public async Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user)
    {
        var httpCtx = _http.HttpContext;
        if (httpCtx?.Items.TryGetValue(CacheKey, out var cached) == true && cached is HashSet<string> set)
            return set;

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (user?.Identity?.IsAuthenticated != true)
        {
            Store(httpCtx, result);
            return result;
        }

        if (AppRoles.IsGlobalAdmin(user))
        {
            foreach (var a in AppActions.AllKnown) result.Add(a);
            Store(httpCtx, result);
            return result;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId))
        {
            ApplyDefaultsByModule(result, await _modules.GetAllowedModulesAsync(user));
            Store(httpCtx, result);
            return result;
        }

        Guid? roleId = await _db.UserPermissionRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.PermissionRoleId)
            .FirstOrDefaultAsync();

        if (roleId == null)
        {
            roleId = await _db.PermissionRoles
                .AsNoTracking()
                .Where(r => r.IsDefault && r.IsActive)
                .OrderByDescending(r => r.UpdatedAt)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync();
        }

        if (roleId != null)
        {
            var actions = await _db.PermissionRoleActions
                .AsNoTracking()
                .Where(x => x.PermissionRoleId == roleId.Value)
                .Select(x => x.ActionKey)
                .ToListAsync();
            foreach (var a in actions)
                if (!string.IsNullOrWhiteSpace(a))
                    result.Add(a.Trim());
        }

        ApplyDefaultsByModule(result, await _modules.GetAllowedModulesAsync(user));

        Store(httpCtx, result);
        return result;
    }

    public async Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey)) return true;
        if (AppRoles.IsGlobalAdmin(user)) return true;
        if (user?.Identity?.IsAuthenticated != true) return false;
        var set = await GetAllowedActionsAsync(user);
        return set.Contains(actionKey);
    }

    private static void Store(HttpContext? ctx, HashSet<string> set)
    {
        if (ctx == null) return;
        ctx.Items[CacheKey] = set;
    }

    private static void ApplyDefaultsByModule(HashSet<string> result, HashSet<string> modules)
    {
        if (modules.Contains(AppModules.Inventory))
            result.Add(AppActions.InventoryView);

        if (modules.Contains(AppModules.Tickets))
            result.Add(AppActions.TicketsView);

        if (modules.Contains(AppModules.Sales))
        {
            result.Add(AppActions.SalesViewOwn);
        }

        if (modules.Contains(AppModules.Billing))
            result.Add(AppActions.BillingViewOwn);

        if (modules.Contains(AppModules.Carriers))
            result.Add(AppActions.CarriersView);

        if (modules.Contains(AppModules.Monitoring))
            result.Add(AppActions.MonitoringView);

        if (modules.Contains(AppModules.Clients))
            result.Add(AppActions.ClientsView);

        if (modules.Contains(AppModules.Projects))
        {
            result.Add(AppActions.ProjectsView);
        }
    }
}
