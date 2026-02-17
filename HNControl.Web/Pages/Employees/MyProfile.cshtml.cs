using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Employees;

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
        if (userId == null) return;

        Profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
