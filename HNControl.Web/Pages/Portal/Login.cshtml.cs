using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Services.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Portal;

public class LoginModel : PageModel
{
    private readonly IClientPortalAccessService _portalAccess;

    public LoginModel(IClientPortalAccessService portalAccess)
    {
        _portalAccess = portalAccess;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required, MaxLength(40)]
        public string Usuario { get; set; } = "";

        [Required, MaxLength(120)]
        public string Password { get; set; } = "";

        public bool Recordarme { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        var auth = await HttpContext.AuthenticateAsync("ClientPortal");
        if (auth.Succeeded && auth.Principal?.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Portal/Index");

        ViewData["ReturnUrl"] = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Page("/Portal/Index") : returnUrl;
        if (!ModelState.IsValid)
            return Page();

        var result = await _portalAccess.ValidateAsync(Input.Usuario, Input.Password);
        if (!result.IsValid || result.Access == null || result.Client == null)
        {
            Error = "Usuario o contraseña inválidos.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Access.Id.ToString()),
            new(ClaimTypes.Name, result.Client.Name),
            new("ClientId", result.Client.Id.ToString()),
            new("ClientCode", result.Client.ClientCode ?? ""),
            new("ClientUsername", result.Access.Username)
        };

        var identity = new ClaimsIdentity(claims, "ClientPortal");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            "ClientPortal",
            principal,
            new AuthenticationProperties
            {
                IsPersistent = Input.Recordarme,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(Input.Recordarme ? 30 : 1)
            });

        await _portalAccess.MarkLoginAsync(result.Access.Id);

        if (!Url.IsLocalUrl(returnUrl) || !returnUrl.StartsWith("/Portal", StringComparison.OrdinalIgnoreCase))
            returnUrl = Url.Page("/Portal/Index");

        return LocalRedirect(returnUrl!);
    }
}
