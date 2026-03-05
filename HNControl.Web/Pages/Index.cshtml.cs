using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.IsInRole(AppRoles.Admin))
            return RedirectToPage("/Admin/Dashboard");

        if (User.IsInRole(AppRoles.Employee))
            return RedirectToPage("/Employees/MyProfile");

        return RedirectToPage("/Account/Login");
    }
}
