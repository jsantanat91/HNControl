using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Employees;

[Authorize]
public class OrgChartModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;

    public OrgChartModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    public List<EmployeeVm> Employees { get; set; } = [];
    public List<NodeVm> Nodes { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AppRoles.IsGlobalAdmin(User) && !await _actions.HasActionAsync(User, AppActions.EmployeesOrgChartView))
            return Forbid();

        Employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new EmployeeVm
            {
                UserId = x.UserId,
                FullName = x.FullName,
                Position = x.Position,
                Email = x.Email
            })
            .ToListAsync();

        Nodes = await _db.EmployeeOrgChartNodes
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new NodeVm
            {
                UserId = x.UserId,
                ReportsToUserId = x.ReportsToUserId,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return Page();
    }

    public class EmployeeVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Position { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class NodeVm
    {
        public string UserId { get; set; } = "";
        public string? ReportsToUserId { get; set; }
        public int SortOrder { get; set; }
    }
}
