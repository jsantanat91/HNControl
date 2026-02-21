using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Carriers;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<InternetCarrier> Carriers { get; set; } = new();

    public async Task OnGetAsync()
    {
        Carriers = await _db.InternetCarriers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}
