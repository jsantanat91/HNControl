using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly IWhatsAppSender _whatsApp;
    private readonly IDataProtector _protector;
    private const string TrustedCookieName = "HNControl.Trusted2FA";
    private const string RememberedEmailCookieName = "HNControl.LoginEmail";

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IEmailSender email,
        IWhatsAppSender whatsApp,
        IDataProtectionProvider dataProtectionProvider)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _email = email;
        _whatsApp = whatsApp;
        _protector = dataProtectionProvider.CreateProtector("HNControl.Login.TwoFactorTrust.v1");
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Error { get; set; }
    public string? Info { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; } = true;

        [RegularExpression(@"^\d{8}$", ErrorMessage = "El código debe tener 8 dígitos.")]
        public string? TwoFactorCode { get; set; }
        public Guid? ChallengeId { get; set; }
        public bool AwaitingTwoFactor { get; set; }
    }

    public void OnGet()
    {
        Input.Email = GetRememberedEmail();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.AwaitingTwoFactor && Input.ChallengeId.HasValue)
            return await VerifyTwoFactorAsync();

        if (!ModelState.IsValid) return Page();

        Input.Email = (Input.Email ?? "").Trim();
        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            Error = "Credenciales inválidas.";
            return Page();
        }

        var passCheck = await _signInManager.CheckPasswordSignInAsync(user, Input.Password ?? "", lockoutOnFailure: true);
        if (!passCheck.Succeeded)
        {
            Error = passCheck.IsLockedOut
                ? "Usuario bloqueado temporalmente por intentos fallidos."
                : "Credenciales inválidas.";
            return Page();
        }

        PersistRememberedEmail(Input.Email, Input.RememberMe);

        var ip = GetClientIp();
        if (IsTrustedDevice(user.Id, ip))
        {
            await _signInManager.SignInAsync(user, Input.RememberMe);
            return await RedirectByRoleAsync(user);
        }

        // Limpieza de retos antiguos para no crecer la tabla.
        var oldChallenges = await _db.LoginTwoFactorChallenges
            .Where(x => x.ExpiresAt < DateTime.UtcNow.AddDays(-1) || x.UsedAt != null)
            .ToListAsync();
        if (oldChallenges.Count > 0)
        {
            _db.LoginTwoFactorChallenges.RemoveRange(oldChallenges);
            await _db.SaveChangesAsync();
        }

        var code = RandomNumberGenerator.GetInt32(10000000, 100000000).ToString();
        var challenge = new LoginTwoFactorChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserEmail = user.Email ?? Input.Email,
            IpAddress = ip,
            CodeHash = HashCode(code),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            FailedAttempts = 0
        };
        _db.LoginTwoFactorChallenges.Add(challenge);
        await _db.SaveChangesAsync();

        var delivery = await SendTwoFactorCodeAsync(user, challenge.UserEmail, code);
        if (!delivery.Sent)
        {
            _db.LoginTwoFactorChallenges.Remove(challenge);
            await _db.SaveChangesAsync();
            Error = $"No se pudo enviar el código de acceso por correo ni WhatsApp. Detalle: {delivery.Error}";
            return Page();
        }

        Input.AwaitingTwoFactor = true;
        Input.ChallengeId = challenge.Id;
        Input.Password = string.Empty;
        Info = delivery.InfoMessage;
        ModelState.Clear();
        return Page();
    }

    private async Task<TwoFactorDeliveryResult> SendTwoFactorCodeAsync(ApplicationUser user, string email, string code)
    {
        var emailSent = false;
        var whatsAppSent = false;
        var errors = new List<string>();

        try
        {
            await _email.SendAsync(
                email,
                "Código de seguridad HN Control",
                $"<p>Tu código de acceso es:</p><h2 style=\"letter-spacing:2px\">{code}</h2><p>Válido por 10 minutos.</p>");
            emailSent = true;
        }
        catch (Exception ex)
        {
            errors.Add($"Correo: {ex.Message}");
        }

        var phone = await GetTwoFactorPhoneAsync(user);
        if (!string.IsNullOrWhiteSpace(phone) && await _whatsApp.IsConfiguredAsync())
        {
            try
            {
                await _whatsApp.SendAsync(
                    phone,
                    $"Codigo de seguridad HN Control: {code}. Valido por 10 minutos.");
                whatsAppSent = true;
            }
            catch (Exception ex)
            {
                errors.Add($"WhatsApp: {ex.Message}");
            }
        }

        if (emailSent && whatsAppSent)
            return new(true, "Te enviamos un código de 8 dígitos a tu correo y WhatsApp.", "");

        if (emailSent)
            return new(true, "Te enviamos un código de 8 dígitos a tu correo.", "");

        if (whatsAppSent)
            return new(true, "Te enviamos un código de 8 dígitos por WhatsApp.", "");

        if (string.IsNullOrWhiteSpace(phone))
            errors.Add("WhatsApp: el empleado no tiene teléfono registrado.");

        return new(false, "", string.Join(" | ", errors));
    }

    private async Task<string?> GetTwoFactorPhoneAsync(ApplicationUser user)
    {
        var profilePhone = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Phone)
            .FirstOrDefaultAsync();

        return !string.IsNullOrWhiteSpace(profilePhone)
            ? profilePhone
            : user.PhoneNumber;
    }

    private async Task<IActionResult> VerifyTwoFactorAsync()
    {
        if (!Input.ChallengeId.HasValue)
        {
            Error = "Sesión de validación inválida.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.TwoFactorCode) || Input.TwoFactorCode.Length != 8)
        {
            Error = "Ingresa el código de 8 dígitos.";
            Input.AwaitingTwoFactor = true;
            return Page();
        }

        var challenge = await _db.LoginTwoFactorChallenges
            .FirstOrDefaultAsync(x => x.Id == Input.ChallengeId.Value);
        if (challenge == null)
        {
            Error = "El código ya no existe. Inicia sesión otra vez.";
            Input.AwaitingTwoFactor = false;
            return Page();
        }

        if (challenge.UsedAt != null || challenge.ExpiresAt < DateTime.UtcNow)
        {
            Error = "El código expiró. Inicia sesión nuevamente para generar otro.";
            Input.AwaitingTwoFactor = false;
            return Page();
        }

        var ip = GetClientIp();
        if (!string.Equals(challenge.IpAddress, ip, StringComparison.OrdinalIgnoreCase))
        {
            Error = "El código es válido solo para la misma IP pública.";
            Input.AwaitingTwoFactor = false;
            return Page();
        }

        if (!string.Equals(challenge.CodeHash, HashCode(Input.TwoFactorCode), StringComparison.Ordinal))
        {
            challenge.FailedAttempts += 1;
            if (challenge.FailedAttempts >= 5)
                challenge.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _db.SaveChangesAsync();
            Error = "Código incorrecto.";
            Input.AwaitingTwoFactor = true;
            return Page();
        }

        var user = await _userManager.FindByIdAsync(challenge.UserId);
        if (user == null)
        {
            Error = "Usuario no encontrado.";
            Input.AwaitingTwoFactor = false;
            return Page();
        }

        challenge.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _signInManager.SignInAsync(user, Input.RememberMe);
        SetTrustedDevice(user.Id, ip);
        return await RedirectByRoleAsync(user);
    }

    private async Task<IActionResult> RedirectByRoleAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
            return RedirectToPage("/Admin/Dashboard");

        return RedirectToPage("/Employees/MyProfile");
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    }

    private static string HashCode(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private bool IsTrustedDevice(string userId, string ip)
    {
        if (!Request.Cookies.TryGetValue(TrustedCookieName, out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<TrustPayload>(_protector.Unprotect(protectedValue));
            return payload != null
                && string.Equals(payload.UserId, userId, StringComparison.Ordinal)
                && string.Equals(payload.Ip, ip, StringComparison.OrdinalIgnoreCase)
                && payload.ExpiresAtUtc > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private void SetTrustedDevice(string userId, string ip)
    {
        var payload = new TrustPayload
        {
            UserId = userId,
            Ip = ip,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        var token = _protector.Protect(JsonSerializer.Serialize(payload));
        Response.Cookies.Append(TrustedCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = payload.ExpiresAtUtc
        });
    }

    private void PersistRememberedEmail(string email, bool remember)
    {
        if (!remember || string.IsNullOrWhiteSpace(email))
        {
            Response.Cookies.Delete(RememberedEmailCookieName);
            return;
        }

        Response.Cookies.Append(RememberedEmailCookieName, email.Trim(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    private string GetRememberedEmail()
    {
        if (!Request.Cookies.TryGetValue(RememberedEmailCookieName, out var email))
            return string.Empty;
        return (email ?? string.Empty).Trim();
    }

    private sealed class TrustPayload
    {
        public string UserId { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }

    private sealed record TwoFactorDeliveryResult(bool Sent, string InfoMessage, string Error);
}
