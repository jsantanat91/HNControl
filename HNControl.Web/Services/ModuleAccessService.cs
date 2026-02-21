using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class ModuleAccessService : IModuleAccessService
{
    private const string CacheKey = "__hn_allowed_modules";
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public ModuleAccessService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task<HashSet<string>> GetAllowedModulesAsync(ClaimsPrincipal user)
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

        if (user.IsInRole(AppRoles.Admin))
        {
            foreach (var k in AppModules.AllKnown) result.Add(k);
            Store(httpCtx, result);
            return result;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrWhiteSpace(userId))
        {
            foreach (var k in AppModules.EmployeeDefaults) result.Add(k);
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

        if (roleId == null)
        {
            foreach (var k in AppModules.EmployeeDefaults) result.Add(k);
            Store(httpCtx, result);
            return result;
        }

        var modules = await _db.PermissionRoleModules
            .AsNoTracking()
            .Where(m => m.PermissionRoleId == roleId)
            .Select(m => m.ModuleKey)
            .ToListAsync();

        foreach (var m in modules)
            if (!string.IsNullOrWhiteSpace(m))
                result.Add(m);

        if (result.Count == 0)
            foreach (var k in AppModules.EmployeeDefaults) result.Add(k);

        Store(httpCtx, result);
        return result;
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey)) return true;
        if (user?.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole(AppRoles.Admin)) return true;

        var set = await GetAllowedModulesAsync(user);
        return set.Contains(moduleKey);
    }

    private static void Store(HttpContext? ctx, HashSet<string> set)
    {
        if (ctx == null) return;
        ctx.Items[CacheKey] = set;
    }
}
