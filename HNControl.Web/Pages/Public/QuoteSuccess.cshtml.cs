using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Public;

[AllowAnonymous]
public class QuoteSuccessModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Folio { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int Sent { get; set; }

    public bool SentOk => Sent == 1;

    public void OnGet()
    {
        if (string.IsNullOrWhiteSpace(Folio))
            Folio = "Sin folio";
    }
}
