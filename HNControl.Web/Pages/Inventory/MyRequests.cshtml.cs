using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class MyRequestsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public MyRequestsModel(ApplicationDbContext db) => _db = db;

    public List<InventoryMovement> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        Rows = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .Where(m => m.RequestedByUserId == userId || m.ResponsibleUserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .Take(300)
            .ToListAsync();
    }
}
