using System.Security.Claims;

namespace HNControl.Web.Services;

public interface IActionAccessService
{
    Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user);
    Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey);
}

