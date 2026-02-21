using System.Security.Claims;

namespace HNControl.Web.Services;

public interface IModuleAccessService
{
    Task<HashSet<string>> GetAllowedModulesAsync(ClaimsPrincipal user);
    Task<bool> HasAccessAsync(ClaimsPrincipal user, string moduleKey);
}
