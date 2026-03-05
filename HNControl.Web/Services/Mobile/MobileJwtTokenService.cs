using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace HNControl.Web.Services.Mobile;

public class MobileJwtTokenService
{
    private readonly IConfiguration _cfg;
    private readonly UserManager<ApplicationUser> _userManager;

    public MobileJwtTokenService(IConfiguration cfg, UserManager<ApplicationUser> userManager)
    {
        _cfg = cfg;
        _userManager = userManager;
    }

    public async Task<(string token, DateTime expiresAtUtc)> CreateAsync(ApplicationUser user)
    {
        var issuer = _cfg["Jwt:Issuer"] ?? "HNControl.Mobile";
        var audience = _cfg["Jwt:Audience"] ?? "HNControl.Mobile";
        var key = _cfg["Jwt:Key"] ?? "DEV_ONLY_CHANGE_THIS_KEY_32_CHARS_MIN";
        var expMinutes = int.TryParse(_cfg["Jwt:ExpiresMinutes"], out var x) ? x : 480;

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(expMinutes);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
