using HNControl.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public int EmployeeCount { get; set; }
    public int ViaticWeekCount { get; set; }

    public async Task OnGetAsync()
    {
        EmployeeCount = await _db.EmployeeProfiles.CountAsync();
        ViaticWeekCount = await _db.ViaticWeeks.CountAsync();
    }
}
