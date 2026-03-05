namespace HNControl.Mobile.Services;

public sealed class MobileApiSettings
{
    private const string BaseUrlKey = "mobile_api_base_url";
    private const string DefaultBaseUrl = "http://10.0.2.2:5128";

    public string BaseUrl
    {
        get => Preferences.Get(BaseUrlKey, DefaultBaseUrl);
        set
        {
            var clean = (value ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(clean))
            {
                clean = DefaultBaseUrl;
            }

            Preferences.Set(BaseUrlKey, clean);
        }
    }
}
