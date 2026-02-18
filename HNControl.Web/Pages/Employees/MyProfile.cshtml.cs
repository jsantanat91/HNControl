using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Employees;

[Authorize(Policy = "EmployeeOnly")]
public class MyProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly ApplicationDbContext _db;

    public MyProfileModel(UserManager<ApplicationUser> userMgr, ApplicationDbContext db)
    {
        _userMgr = userMgr;
        _db = db;
    }

    public EmployeeProfile? Profile { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return;

        Profile = await _db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
