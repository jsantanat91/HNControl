using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class AuthSession
{
    private const string TokenKey = "mobile_auth_token";
    private const string UserNameKey = "mobile_auth_name";
    private const string UserEmailKey = "mobile_auth_email";
    private const string ExpiresAtKey = "mobile_auth_expires";

    public string Token { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Token) && ExpiresAtUtc > DateTime.UtcNow.AddMinutes(-1);

    public AuthSession()
    {
        Token = Preferences.Get(TokenKey, "");
        FullName = Preferences.Get(UserNameKey, "");
        Email = Preferences.Get(UserEmailKey, "");
        var expiresRaw = Preferences.Get(ExpiresAtKey, "");
        if (!DateTime.TryParse(expiresRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expires))
        {
            expires = DateTime.UtcNow.AddMinutes(-5);
        }
        ExpiresAtUtc = expires;
    }

    public void Set(LoginResponseDto login)
    {
        Token = login.Token ?? "";
        FullName = login.FullName ?? "";
        Email = login.Email ?? "";
        ExpiresAtUtc = login.ExpiresAtUtc;

        Preferences.Set(TokenKey, Token);
        Preferences.Set(UserNameKey, FullName);
        Preferences.Set(UserEmailKey, Email);
        Preferences.Set(ExpiresAtKey, ExpiresAtUtc.ToString("O"));
    }

    public void Clear()
    {
        Token = "";
        FullName = "";
        Email = "";
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);

        Preferences.Remove(TokenKey);
        Preferences.Remove(UserNameKey);
        Preferences.Remove(UserEmailKey);
        Preferences.Remove(ExpiresAtKey);
    }
}
