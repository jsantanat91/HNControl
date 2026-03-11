using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(string UserId, string FullName, string Email, string Position, decimal SalaryBase, bool IsInventoryManager);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var inventoryRoleId = await _db.Roles
            .Where(r => r.Name == AppRoles.InventoryManager)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var managerUserIds = string.IsNullOrWhiteSpace(inventoryRoleId)
            ? new HashSet<string>()
            : (await _db.UserRoles
                .Where(ur => ur.RoleId == inventoryRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync()).ToHashSet();

        var employees = await _db.EmployeeProfiles
            .OrderBy(e => e.FullName)
            .ToListAsync();

        Rows = employees
            .Select(e => new Row(
                e.UserId,
                e.FullName,
                e.Email,
                e.Position,
                e.SalaryBase,
                managerUserIds.Contains(e.UserId)))
            .ToList();
    }
}
