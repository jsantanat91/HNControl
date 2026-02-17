using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Employees;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) { _db = db; }

    public List<EmployeeProfile> Employees { get; set; } = new();

    public async Task OnGetAsync()
    {
        Employees = await _db.EmployeeProfiles
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }
}
