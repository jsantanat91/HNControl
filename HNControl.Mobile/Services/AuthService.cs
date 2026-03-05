using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class AuthService
{
    private readonly MobileApiSettings _settings;
    private readonly MobileApiClient _api;
    private readonly AuthSession _session;

    public AuthService(MobileApiSettings settings, MobileApiClient api, AuthSession session)
    {
        _settings = settings;
        _api = api;
        _session = session;
    }

    public string CurrentBaseUrl
    {
        get => _settings.BaseUrl;
        set => _settings.BaseUrl = value;
    }

    public AuthSession Session => _session;

    public async Task LoginAsync(string baseUrl, string email, string password)
    {
        _settings.BaseUrl = baseUrl;
        var response = await _api.PostJsonAsync<LoginRequestDto, LoginResponseDto>(
            "api/mobile/auth/login",
            new LoginRequestDto
            {
                Email = (email ?? "").Trim(),
                Password = password ?? ""
            },
            withAuth: false);

        _session.Set(response);
    }

    public void Logout()
    {
        _session.Clear();
    }
}
