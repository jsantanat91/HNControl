using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class ProspectsModel : PageModel
{
    public IActionResult OnGet()
    {
        return Redirect("/Clients/Index?View=leads");
    }
}

