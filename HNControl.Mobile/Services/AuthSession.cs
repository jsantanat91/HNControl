using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class AuthSession
{
    private const string TokenKey = "mobile_auth_token";
    private const string UserNameKey = "mobile_auth_name";
    private const string UserEmailKey = "mobile_auth_email";
    private const string ExpiresAtKey = "mobile_auth_expires";
    private const string RememberMeKey = "mobile_auth_remember_me";
    private const string SavedUserKey = "mobile_auth_saved_user";
    private const string SavedPasswordKey = "mobile_auth_saved_password";

    public string Token { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Token) && ExpiresAtUtc > DateTime.UtcNow.AddMinutes(-1);
    public bool RememberMe => Preferences.Get(RememberMeKey, false);

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

    public async Task<(bool Remember, string User, string Password)> LoadSavedCredentialsAsync()
    {
        var remember = Preferences.Get(RememberMeKey, false);
        if (!remember) return (false, "", "");

        var user = Preferences.Get(SavedUserKey, "");
        string password = "";
        try
        {
            password = await SecureStorage.Default.GetAsync(SavedPasswordKey) ?? "";
        }
        catch
        {
            password = "";
        }

        return (true, user, password);
    }

    public async Task SaveCredentialsAsync(string user, string password, bool remember)
    {
        Preferences.Set(RememberMeKey, remember);
        if (!remember)
        {
            Preferences.Remove(SavedUserKey);
            try { SecureStorage.Default.Remove(SavedPasswordKey); } catch { }
            return;
        }

        Preferences.Set(SavedUserKey, user ?? "");
        try
        {
            await SecureStorage.Default.SetAsync(SavedPasswordKey, password ?? "");
        }
        catch
        {
            // Ignora error de secure storage (emuladores/dispositivos restringidos).
        }
    }
}
