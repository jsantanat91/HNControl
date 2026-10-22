using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Admin.SystemPages;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.SuperAdmin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class HealthModel(SystemHealthReader reader) : PageModel
{
    public SystemHealthReader.Snapshot Health { get; private set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();
        Health = await reader.ReadAsync(HttpContext.RequestAborted);
        return Page();
    }
}
