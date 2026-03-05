using HNControl.Web.Models;
using HNControl.Web.Services.Mobile;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace HNControl.Web.Controllers.Mobile;

[ApiController]
[Route("api/mobile/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MobileJwtTokenService _jwt;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        MobileJwtTokenService jwt)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _jwt = jwt;
    }

    public record LoginRequest(string Email, string Password);
    public record LoginResponse(string Token, DateTime ExpiresAtUtc, string UserId, string Email, string FullName, string[] Roles);

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var email = (req.Email ?? "").Trim();
        var pwd = req.Password ?? "";

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return Unauthorized(new { message = "Credenciales invalidas" });

        var ok = await _signInManager.CheckPasswordSignInAsync(user, pwd, lockoutOnFailure: true);
        if (!ok.Succeeded) return Unauthorized(new { message = "Credenciales invalidas" });

        var roles = await _userManager.GetRolesAsync(user);
        var (token, exp) = await _jwt.CreateAsync(user);

        return Ok(new LoginResponse(
            token,
            exp,
            user.Id,
            user.Email ?? "",
            user.UserName ?? user.Email ?? "",
            roles.ToArray()
        ));
    }
}
